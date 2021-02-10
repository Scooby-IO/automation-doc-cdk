using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoDemo
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new AutoDemoStack(app, "AutoDemoStack");
            app.Synth();
        }
    }
}
