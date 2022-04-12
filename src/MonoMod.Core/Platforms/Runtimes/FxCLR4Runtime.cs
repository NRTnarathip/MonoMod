﻿using MonoMod.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MonoMod.Core.Platforms.Runtimes {
    internal class FxCLR4Runtime : FxBaseRuntime {
        public override void DisableInlining(MethodBase method) {
            // the base classes don't specify RuntimeFeature.DisableInlining, so this should never be called
            throw new PlatformNotSupportedException();
        }
    }
}
