using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nimbus.Tests.Common.TestScenarioGeneration.ConfigurationSources;
using NUnit.Framework;

namespace Nimbus.Tests.Common.TestScenarioGeneration.TestCaseSources
{
    public class AllBusConfigurations<TTestType> : IEnumerable<TestCaseData>
    {
        public IEnumerator<TestCaseData> GetEnumerator()
        {
            var testCases = new BusBuilderConfigurationSources(typeof (TTestType))
                .Select(scenario => scenario.BuildTestCase())
                .OrderBy(tc => tc.TestName)
                .ToArray();

            return testCases.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}