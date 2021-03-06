﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace CudaLearn.Tests
{

    public class StocasticPoolingLayerTests : CpuLayerTests
    {
        private readonly Tensor bottom = new Tensor(2, 3, 6, 5);
        private readonly Tensor top = new Tensor();

        public StocasticPoolingLayerTests()
        {
            var filler = new UniformFiller(0.1f, 1f);
            filler.Fill(bottom);
        }

        [Fact]
        public void StocasticPoolingLayer_Setup()
        {
            var layer = new StocasticPoolingLayer(3, 2);
            layer.Setup(bottom, top);

            Assert.Equal(bottom.Num, top.Num);
            Assert.Equal(bottom.Channels, top.Channels);
            Assert.Equal(3, top.Height);
            Assert.Equal(2, top.Width);
        }

        [Fact]
        public void StocasticPoolingLayer_Forward()
        {
            Context.Instance.Phase = PhaseType.Train;

            var layer = new StocasticPoolingLayer(3, 2);           
            layer.Setup(bottom, top);
            layer.Forward(bottom, top);

            int num = top.Num;
            int channels = top.Channels;
            int height = top.Height;
            int width = top.Width;

            using (var topCpu = top.OnCpu())
            using (var bottomCpu = bottom.OnCpu())
            {
                var topData = topCpu.Data;
                var bottomData = bottomCpu.Data;

                for (int n = 0; n < num; n++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        for (int ph = 0; ph < height; ph++)
                        {
                            for (int pw = 0; pw < width; pw++)
                            {
                                double pooled = topData[topCpu.Offset(n, c, ph, pw)];

                                int hstart = ph * 2;
                                int hend = Math.Min(hstart + 3, bottom.Height);
                                int wstart = pw * 2;
                                int wend = Math.Min(wstart + 3, bottom.Width);

                                bool hasEqual = false;
                                for (int h = hstart; h < hend; ++h)
                                {
                                    for (int w = wstart; w < wend; ++w)
                                    {
                                        hasEqual |= (pooled == bottomData[bottomCpu.Offset(n, c, h, w)]);
                                    }
                                }
                                Assert.True(hasEqual);
                            }
                        }
                    }
                }
            }
        }
    }
}
