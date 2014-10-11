using System.IO;
using AtHangar;

namespace KSPAVCupdater
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			using(var file = new StreamWriter(KSP_AVC_Info.VersionFile))
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
			file.WriteLine("         \"MAJOR\":{0}", KSP_AVC_Info.MinKSPVersion.Major);
			file.WriteLine("         \"MINOR\":{0}", KSP_AVC_Info.MinKSPVersion.Minor);
			file.WriteLine("         \"PATCH\":{0}", KSP_AVC_Info.MinKSPVersion.Build);
			file.WriteLine(
@"     }
    ""KSP_VERSION_MAX"":
     {");
			file.WriteLine("         \"MAJOR\":{0}", KSP_AVC_Info.MaxKSPVersion.Major);
			file.WriteLine("         \"MINOR\":{0}", KSP_AVC_Info.MaxKSPVersion.Minor);
			file.WriteLine("         \"PATCH\":{0}", KSP_AVC_Info.MaxKSPVersion.Build);
			file.WriteLine(
@"     }
}");
			}
		}
	}
}
