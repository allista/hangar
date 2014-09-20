using System.Reflection;

// Information about this assembly is defined by the following attributes. 
// Change them to the values specific to your project.

[assembly: AssemblyTitle("Hangar")]
[assembly: AssemblyDescription("Plugin for the Kerbal Space Program")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("")]
[assembly: AssemblyCopyright("Allis Tauri")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

[assembly: AssemblyVersion("1.2.0.0")]

// The following attributes are used to specify the signing key for the assembly, 
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

namespace AtHangar
{
	public static class KSP_AVC_Info
	{
		public static readonly System.Version HangarVersion = Assembly.GetCallingAssembly().GetName().Version;
		public static readonly Version MinKSPVersion = new Version(0,24,2);
		public static readonly Version MaxKSPVersion = new Version(0,24,2);
		public static readonly string  VersionURL    = "https://raw.githubusercontent.com/allista/hangar/master/GameData/Hangar/Hangar.version";
		public static readonly string  UpgradeURL    = "https://github.com/allista/hangar/releases";
		public static readonly string  VersionFile   = "Hangar.version";
	}
}