﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Models
{
    public class AgentTestRunResult
    {
        public string TestSuiteId { get; set; }
        public List<TestExecutionInfo> TestResults { get; set; }
    }
}
