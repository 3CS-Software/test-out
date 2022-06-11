using System;
using System.IO;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Execution
{
    /// <summary>
    /// Validates the paths from the injected config, and returns the validated and curated paths.
    /// </summary>
    public class InvocationPathValidator
    {
        readonly InvokerConfig _config;

        public class ValidatedPaths
        {
            /// <summary>
            /// The full path to the folder that is going to be transmitted.
            /// </summary>
            public string BasePath { get; set; }

            /// <summary>
            /// The relative path from the base path for the test assembly.
            /// </summary>
            public string TestAssemblyPath { get; set; }
        }

        public InvocationPathValidator(InvokerConfig config)
        {
            _config = config;
        }

        public ValidatedPaths GetValidPaths()
        {
            //Validate the paths.
            string effectiveBasePath;
            if (_config.BasePath != null)
            {
                effectiveBasePath = Path.GetFullPath(_config.BasePath);
            }
            else
            {
                effectiveBasePath = Path.GetDirectoryName(_config.TestAssemblyPath);
            }

            if (string.IsNullOrEmpty(_config.TestAssemblyPath))
            {
                throw new InvalidOperationException("TestAssemblyPath cannot be empty.");
            }

            string testAssemblyFullPath;
            if (Path.IsPathFullyQualified(_config.TestAssemblyPath))
            {
                testAssemblyFullPath = _config.TestAssemblyPath;
            }
            else
            {
                testAssemblyFullPath = Path.GetFullPath(Path.Combine(effectiveBasePath, _config.TestAssemblyPath));
            }

            if (!testAssemblyFullPath.ToUpper().StartsWith(effectiveBasePath.ToUpper()))
            {
                throw new InvalidOperationException("Test Assembly Path must be containd in Base Path");
            }


            var relativeTestAssemblyPath = "." + testAssemblyFullPath.Substring(effectiveBasePath.Length);
            return new ValidatedPaths
            {
                BasePath = effectiveBasePath,
                TestAssemblyPath = relativeTestAssemblyPath
            };
        }
    }
}