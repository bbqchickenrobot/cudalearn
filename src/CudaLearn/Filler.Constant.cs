﻿using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CudaLearn
{
    public class ConstantFillerConfiguration : FillerConfiguration
    {
        public ConstantFillerConfiguration() : this(0.0d) { }

        public ConstantFillerConfiguration(double value)
            : base(FillerType.Constant)
        {
            this.Value = value;
        }

        public double Value { get; set; }
    }

    public class ConstantFiller : Filler<ConstantFillerConfiguration>
    {
        public ConstantFiller(double c)
            : this(new ConstantFillerConfiguration(c))
        { }

        public ConstantFiller()
            : this(new ConstantFillerConfiguration())
        { }

        public ConstantFiller(ConstantFillerConfiguration param)
            : base(param)
        { }

        public override void Fill(Tensor blob)
        {
            using (var @cpuBlob = blob.OnCpu())
            {
                var data = @cpuBlob.Data;

                var value = this.Parameters.Value;

                data.MapInplace(x => value, Zeros.Include);
            }
        }
    }
}
