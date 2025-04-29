using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SyncTrayzor.Services
{
    public interface IAssemblyProvider
    {
        Version Version { get; }
        Version FullVersion { get; }
        string Location { get; }
        Architecture ProcessorArchitecture { get; }
        string FrameworkDescription { get; }
        Stream GetManifestResourceStream(string path);
    }

    public class AssemblyProvider : IAssemblyProvider
    {
        private readonly Assembly assembly;

        public AssemblyProvider()
        {
            assembly = Assembly.GetExecutingAssembly();

            // Don't include the revision in this version
            var version = assembly.GetName().Version ?? Version.Parse("0.0.0");
            Version = new Version(version.Major, version.Minor, version.Build);
        }

        public Version Version { get; }

        public Version FullVersion => assembly.GetName().Version;

        public string Location => assembly.Location;

        public Architecture ProcessorArchitecture => RuntimeInformation.ProcessArchitecture;

        public string FrameworkDescription => RuntimeInformation.FrameworkDescription;

        public Stream GetManifestResourceStream(string path)
        {
            return assembly.GetManifestResourceStream(path);
        }
    }
}
