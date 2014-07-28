using System.Reflection;
using System.IO;
using AtHangar;

namespace KSPAVCupdater
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			using(StreamWriter file = new StreamWriter(KSP_AVC_Info.VersionFile))
			{
			file.WriteLine(
@"{{ 
    ""NAME"":""Hangar"",
    ""URL"":""{0}"",
    ""DOWNLOAD"":""{1}"",
    ""VERSION"":
     {{", KSP_AVC_Info.VersionURL, KSP_AVC_Info.UpgradeURL);
			file.WriteLine("         \"MAJOR\":{0}", KSP_AVC_Info.HangarVersion.Major);
			file.WriteLine("         \"MINOR\":{0}", KSP_AVC_Info.HangarVersion.Minor);
			file.WriteLine("         \"PATCH\":{0}", KSP_AVC_Info.HangarVersion.Build);
			file.WriteLine("         \"BUILD\":{0}", KSP_AVC_Info.HangarVersion.Revision);
			file.WriteLine(
@"     }
    ""KSP_VERSION_MIN"":
     {");
			file.WriteLine("         \"MAJOR\":{0}", KSP_AVC_Info.MinKSPVersion.major);
			file.WriteLine("         \"MINOR\":{0}", KSP_AVC_Info.MinKSPVersion.minor);
			file.WriteLine("         \"PATCH\":{0}", KSP_AVC_Info.MinKSPVersion.revision);
			file.WriteLine(
@"     }
    ""KSP_VERSION_MAX"":
     {");
			file.WriteLine("         \"MAJOR\":{0}", KSP_AVC_Info.MaxKSPVersion.major);
			file.WriteLine("         \"MINOR\":{0}", KSP_AVC_Info.MaxKSPVersion.minor);
			file.WriteLine("         \"PATCH\":{0}", KSP_AVC_Info.MaxKSPVersion.revision);
			file.WriteLine(
@"     }
}");
			}
		}
	}
}
