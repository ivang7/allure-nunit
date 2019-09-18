using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Allure.Commons;
using AspectInjector.Broker;
using SmartFormat;

namespace NUnit.Allure.Steps
{
    [Aspect(Scope.Global)]
    public class AllureStepAspect
    {
        [Advice(Kind.Around, Targets = Target.Method)]
        public object WrapStep(
            [Argument(Source.Name)] string name,
            [Argument(Source.Metadata)] MethodBase methodBase,
            [Argument(Source.Arguments)] object[] arguments,
            [Argument(Source.Target)] Func<object[], object> method)
        {
            var stepName = methodBase.GetCustomAttribute<AllureStepAttribute>().StepName;
            var parametersWithValue = this.GetArgumentsDictionary(methodBase, arguments);

            var stepResult = string.IsNullOrEmpty(stepName)
                ? new StepResult { name = name }
                : new StepResult { name = Smart.Format(stepName, parametersWithValue) };

            stepResult.parameters = parametersWithValue.Select(p => new Parameter { name = p.Key, value = p.Value.ToString() }).ToList();

            object result;
            try
            {
                AllureLifecycle.Instance.StartStep(Guid.NewGuid().ToString(), stepResult);
                result = method(arguments);
                AllureLifecycle.Instance.StopStep(step => stepResult.status = Status.passed);
            }
            catch (Exception e)
            {
                AllureLifecycle.Instance.StopStep(step =>
                {
                    step.statusDetails = new StatusDetails
                    {
                        message = e.Message,
                        trace = e.StackTrace
                    };
                    step.status = Status.failed;
                });
                throw;
            }

            return result;
        }

        private IDictionary<string, object> GetArgumentsDictionary(MethodBase method, object[] arguments)
        {
            var parameters = method.GetParameters();
            var result = parameters.Select(p => p.Name)
                .Zip(arguments, (p, a) => new KeyValuePair<string, object>(p, a))
                .ToDictionary(kv => kv.Key, kv => kv.Value ?? "<null>");

            parameters
                .Where(p => p.CustomAttributes.Select(a => a.AttributeType.Name).Contains("ParamArrayAttribute"))
                .Select(p => p.Name).ToList()
                .ForEach(arg => result[arg] = this.ParamsArgumentToString(result[arg]));

            return result;
        }

        private string ParamsArgumentToString(object obj)
        {
            var result = new List<object>();
            foreach (var o in (IEnumerable)obj)
            {
                result.Add(o as object);
            }

            return "[" + string.Join(", ", result) + "]";
        }
    }
}
