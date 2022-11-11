using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Linq;


namespace Nethereum.RPC.ClassesExtractor
{
    public class RequestInfo
    {
        public RequestInfo()
        {
            BuildRequestParameters = new List<ParameterInfo[]>();
        }

        public Type RequestType { get; set; }
        public Type ReturnType { get; set; }
        public List<ParameterInfo[]> BuildRequestParameters { get; set; }


        public override string ToString()
        {
            var parameterList = new StringBuilder();

            foreach (var buildRequestParameter in BuildRequestParameters)
            {
                parameterList.AppendLine(
                    string.Join(",", buildRequestParameter.OrderBy(x => x.Position).Select(
                    x => { return x.ParameterType.ToString() + " " + x.Name + " " + x.IsOptional + " " + x.DefaultValue; })));
            }
            return "RequestType:" + RequestType.ToString() + Environment.NewLine +
                   "ReturnType:" + ReturnType.ToString() + Environment.NewLine +
                   "Build Requests:" + parameterList.ToString() + Environment.NewLine;

        }
    }
}
