﻿namespace LLMSessionGateway.Core.Utilities.Functional
{
    public readonly struct Unit
    {
        public static readonly Unit Value = new();
        public override string ToString() => "()";
    }
}
