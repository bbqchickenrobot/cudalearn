﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CudaDnn
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CudnnHandle
    {
        public IntPtr Pointer;
    }

}