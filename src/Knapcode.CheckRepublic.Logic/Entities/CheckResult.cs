﻿using System;

namespace Knapcode.CheckRepublic.Logic.Entities
{
    public class CheckResult
    {
        public long CheckResultId { get; set; }
        public long CheckBatchId { get; set; }
        public int CheckId { get; set; }
        public CheckResultType Type { get; set; }
        public string Message { get; set; }
        public long Time { get; set; }
        public long Duration { get; set; }

        public Check Check { get; set; }
        public CheckBatch CheckBatch { get; set; }
    }
}
