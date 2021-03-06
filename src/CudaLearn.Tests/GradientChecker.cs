﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CudaLearn.Tests
{
    internal class GradientChecker
    {       
        private readonly double step;
        private readonly double threshold;
        private readonly int seed;
        private readonly double kink;
        private readonly double kinkRange;

        // kink and kink_range specify an ignored non-smooth region of the form
        // kink - kink_range <= |feature value| <= kink + kink_range,
        // which accounts for all non-smoothness in use
        public GradientChecker(double step, double threshold, int seed = 1701, double kink = 0.0d, double kinkRange = -1.0d)
        {
            this.step = step;
            this.threshold = threshold;
            this.seed = seed;
            this.kink = kink;
            this.kinkRange = kinkRange;
        }

        public void Check ( Layer layer, Tensor bottom, Tensor top, int checkBottom = -1)
        {
            this.Check(layer, new TensorCollection { bottom }, new TensorCollection { top }, checkBottom);
        }

        public void Check(Layer layer, TensorCollection bottom, TensorCollection top, int checkBottom = -1)
        {
            layer.Setup( bottom, top );
            CheckSingle( layer, bottom, top, checkBottom, -1, -1);
        }


        public void CheckExhaustive ( Layer layer, Tensor bottom, Tensor top, int checkBottom = -1)
        {
            this.CheckExhaustive(layer, new TensorCollection { bottom }, new TensorCollection { top }, checkBottom);
        }

        public void CheckExhaustive(Layer layer, TensorCollection bottom, TensorCollection top, int checkBottom = -1)
        {
            layer.Setup( bottom, top );
            Assert.True(top.Count > 0, "Exhaustive mode requires at least one top blob.");

            for (int i = 0; i < top.Count; i++)
                for (int j = 0; j < top[i].Count; j++)
                    CheckSingle(layer, bottom, top, checkBottom, i, j);
        }

        public void CheckEltwise ( Layer layer, Tensor bottom, Tensor top)
        {
            this.CheckEltwise(layer, new TensorCollection { bottom }, new TensorCollection { top });
        }


        public void CheckEltwise(Layer layer, TensorCollection bottom, TensorCollection top)
        {
            layer.Setup(bottom, top);
            Assert.True(top.Count > 0, "Exhaustive mode requires at least one top blob.");

            int checkBottom = -1;
            for (int i = 0; i < top.Count; i++)
                for (int j = 0; j < top[i].Count; j++)
                    CheckSingle(layer, bottom, top, checkBottom, i, j, elementwise: true);
        }
        public void CheckSingle( Layer layer,  Tensor bottom, Tensor top, int checkBottom, int topId, int topDataId, bool elementWise = false)
        {
            this.CheckSingle(layer, new TensorCollection { bottom }, new TensorCollection { top }, checkBottom, topId, topDataId, elementWise);
        }

        public void CheckSingle(Layer layer, TensorCollection bottom, TensorCollection top, int checkBottom, int topId, int topDataId, bool elementwise = false)
        {
            //TODO If implemented at all the ability of the layer to access stored blobs, we need to recheck this.
            if ( elementwise )
            {
                Assert.True(topId >= 0);
                Assert.True(topDataId >= 0);
                
                int topCount = top[topId].Count;
                for (int blobId = 0; blobId < bottom.Count; blobId++)
                    Assert.Equal(topCount, bottom[blobId].Count);
            }

            // First, figure out what blobs we need to check against.
            var blobsToCheck = new TensorCollection();
            var propagateDown = new List<bool>().Repeated(bottom.Count, checkBottom < 0);
            if ( checkBottom < 0 )
            {
                // We are not checking the bottom.
                for (int i = 0; i < bottom.Count; i++)
                    blobsToCheck.Add(bottom[i]);
            }
            else
            {
                // We are checking the bottom, therefore we must ensure that the blob checked exists.
                Assert.True(checkBottom < bottom.Count);
                blobsToCheck.Add(bottom[checkBottom]);
                propagateDown[checkBottom] = true;
            }

            //TODO Add a general random generator that layers should use, to ensure we always apply it when layers are non-deterministic.

            // Compute the gradient analytically using Backward
            // Get any loss from the layer
            double computedObjective = layer.Forward(bottom, top);

            // Get additional loss from the objective
            computedObjective += GetObjectiveAndGradient(top, topId, topDataId);
            layer.Backward(top, propagateDown, bottom);

            // Store computed gradients for all checked blobs
            var computedGradientsBlob = new Tensor[blobsToCheck.Count];
            for ( int blobId = 0; blobId < blobsToCheck.Count; blobId++ )
            {
                var currentBlob = blobsToCheck[blobId];
                computedGradientsBlob[blobId] = new Tensor(currentBlob);

                using (var currentBlobCpu = currentBlob.OnCpu())
                using (var computedGradientsBlobCpu = computedGradientsBlob[blobId].OnCpu())
                {
                    var currentDiff = currentBlobCpu.Diff;
                    var computedGradients = computedGradientsBlobCpu.Data;
                    currentDiff.CopyTo(computedGradients);
                }
            }

            // Compute derivative of top w.r.t. each bottom and parameter input using
            // finite differencing.

            for (int blobId = 0; blobId < blobsToCheck.Count; blobId++ )
            {
                var currentBlob = blobsToCheck[blobId];

                using (var currentBlobCpu = currentBlob.OnCpu())
                using (var computedGradientsBlobCpu = computedGradientsBlob[blobId].OnCpu())
                {
                    var computedGradients = computedGradientsBlobCpu.Data;
                    for (int featId = 0; featId < currentBlob.Count; featId++)
                    {
                        // For an element-wise layer, we only need to do finite differencing to
                        // compute the derivative of topData[top_id][top_data_id] w.r.t.
                        // bottomData[blob_id][i] only for i == top_data_id.  For any other
                        // i != top_data_id, we know the derivative is 0 by definition, and simply
                        // check that that's true.
                        double estimatedGradient = 0;
                        if (!elementwise || featId == topDataId)
                        {
                            //TODO Add a general random generator that layers should use, to ensure we always apply it when layers are non-deterministic.

                            // Do finite differencing.
                            // Compute loss with step-size added to input.
                            currentBlobCpu.Data[featId] += step;
                            double positiveObjective = layer.Forward(bottom, top);
                            positiveObjective += GetObjectiveAndGradient(top, topId, topDataId);

                            // Compute loss with step-size subtracted from input.
                            currentBlobCpu.Data[featId] -= step * 2;

                            //TODO Add a general random generator that layers should use, to ensure we always apply it when layers are non-deterministic.

                            double negativeObjective = layer.Forward(bottom, top);
                            negativeObjective += GetObjectiveAndGradient(top, topId, topDataId);

                            // Recover original input value.
                            currentBlobCpu.Data[featId] += step;
                            estimatedGradient = (positiveObjective - negativeObjective) / step / 2.0d;
                        }

                        double computedGradient = computedGradients[featId];
                        double feature = currentBlobCpu.Data[featId];
                        if (kink - kinkRange > Math.Abs(feature) || Math.Abs(feature) > kink + kinkRange)
                        {
                            // We check relative accuracy, but for too small values, we threshold
                            // the scale factor by 1

                            double scale = Math.Max(Math.Max(Math.Abs(computedGradient), Math.Abs(estimatedGradient)), 1.0d);
                            Assert.InRange(computedGradient - estimatedGradient, -threshold * scale, threshold * scale);
                        }
                    }
                }            
            }

        }

        private double GetObjectiveAndGradient(IList<Tensor> top, int topId, int topDataId)
        {
            double loss = 0;
            if ( topId < 0 )
            {
                // the loss will be half of the sum of squares of all outputs
                for (int i = 0; i < top.Count; i++)
                {
                    using (var topBlobCpu = top[i].OnCpu())
                    {
                        int count = topBlobCpu.Count;
                        for (int j = 0; j < count; j++)
                            loss += topBlobCpu.Data[j] * topBlobCpu.Data[j];

                        topBlobCpu.Data.CopyTo(topBlobCpu.Diff);
                    }
                }
                loss /= 2.0d;
            }
            else
            {
                // the loss will be the top_data_id-th element in the top_id-th blob.
                for (int i = 0; i < top.Count; i++)
                {
                    using (var topCpu = top[i].OnCpu())
                    {
                        topCpu.Diff.Clear();
                    }
                }

                using (var topCpu = top[topId].OnCpu())
                {
                    loss = topCpu.Data[topDataId];
                    topCpu.Diff[topDataId] = 1.0d;
                }
            }
            return loss;
        }
    }
}
