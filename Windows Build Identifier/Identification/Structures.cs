using System;
using System.Collections.Generic;

namespace WindowsBuildIdentifier.Identification
{
    public enum BuildType
    {
        fre,
        chk
    }

    public enum Type
    {
        Client,
        Server,
        ServerV
    }

    public enum Licensing
    {
        Retail,
        OEM,
        Volume
    }

    // Taken from Pivotman319 / BetaWiki project
    // https://docs.google.com/document/d/1hpphLcfgcygpFuXGl9WrXeBpdLECAhXuImmR7uimnZY/edit
    public enum Product
    {
        Undefined = 0x00000000,
        Ultimate = 0x00000001,
        HomeBasic = 0x00000002,
        HomePremium = 0x00000003,
        Enterprise = 0x00000004,
        HomeBasicN = 0x00000005,
        Business = 0x00000006,
        ServerStandard = 0x00000007,
        ServerDatacenter = 0x00000008,
        ServerSmallBusiness = 0x00000009,
        ServerEnterprise = 0x0000000A,
        Starter = 0x0000000B,
        ServerDatacenterCore = 0x0000000C,
        ServerStandardCore = 0x0000000D,
        ServerEnterpriseCore = 0x0000000E,
        ServerEnterpriseIA64 = 0x0000000F,
        BusinessN = 0x00000010,
        ServerWeb = 0x00000011,
        ServerCluster = 0x00000012,
        ServerHome = 0x00000013,
        ServerStorageExpress = 0x00000014,
        ServerStorageStandard = 0x00000015,
        ServerStorageWorkgroup = 0x00000016,
        ServerStorageEnterprise = 0x00000017,
        ServerForSmallBusiness = 0x00000018,
        ServerSmallBusinessPremium = 0x00000019,
        HomePremiumN = 0x0000001A,
        EnterpriseN = 0x0000001B,
        UltimateN = 0x0000001C,
        ServerWebCore = 0x0000001D,
        ServerMediumBusinessManagement = 0x0000001E,
        ServerMediumBusinessSecurity = 0x0000001F,
        ServerMediumBusinessMessaging = 0x00000020,
        ServerFoundation = 0x00000021,
        ServerHomePremium = 0x00000022,
        ServerForSmallBusinessV = 0x00000023,
        ServerStandardV = 0x00000024,
        ServerDatacenterV = 0x00000025,
        ServerEnterpriseV = 0x00000026,
        ServerDatacenterVCor = 0x00000027,
        ServerStandardVCor = 0x00000028,
        ServerEnterpriseVCor = 0x00000029,
        ServerHyperCore = 0x0000002A,
        ServerStorageExpressCore = 0x0000002B,
        ServerStorageStandardCore = 0x0000002C,
        ServerStorageWorkgroupCore = 0x0000002D,
        ServerStorageEnterpriseCore = 0x0000002E,
        StarterN = 0x0000002F,
        Professional = 0x00000030,
        ProfessionalN = 0x00000031,
        ServerSolution = 0x00000032,
        ServerSBSolution = 0x00000033,
        ServerStandardSolution = 0x00000034,
        ServerStandardSolutionCore = 0x00000035,
        ServerSBSolutionEmbedded = 0x00000036,
        ServerForSmallBusinessEmbedded = 0x00000037,
        ServerEmbeddedSolution = 0x00000038,
        ServerEmbeddedSolutionCore = 0x00000039,
        ProfessionalEmbedded = 0x0000003A,
        ServerEssentialBusinessMgmt = 0x0000003B,
        ServerEssentialBusinessAddl = 0x0000003C,
        ServerEssentialBusinessMgmtSvc = 0x0000003D,
        ServerEssentialBusinessAddlSvc = 0x0000003E,
        ServerSmallBusinessPremiumCore = 0x0000003F,
        ServerClusterV = 0x00000040,
        Embedded = 0x00000041,
        StarterE = 0x00000042,
        HomeBasicE = 0x00000043,
        HomePremiumE = 0x00000044,
        ProfessionalE = 0x00000045,
        EnterpriseE = 0x00000046,
        UltimateE = 0x00000047,
        EnterpriseEval = 0x00000048,
        Prerelease = 0x0000004A,
        ServerMultipointStandard = 0x0000004C,
        ServerMultipointPremium = 0x0000004D,
        ServerStandardEval = 0x0000004F,
        ServerDatacenterEval = 0x00000050,
        PrereleaseARM = 0x00000051,
        PrereleaseN = 0x00000052,
        EnterpriseNEval = 0x00000054,
        EmbeddedAutomotive = 0x00000055,
        EmbeddedIndustryA = 0x00000056,
        ThinPC = 0x00000057,
        EmbeddedA = 0x00000058,
        EmbeddedIndustry = 0x00000059,
        EmbeddedE = 0x0000005A,
        EmbeddedIndustryE = 0x0000005B,
        EmbeddedIndustryAE = 0x0000005C,
        ProfessionalPlus = 0x0000005D,
        ServerStorageWorkgroupEval = 0x0000005F,
        ServerStorageStandardEval = 0x00000060,
        CoreARM = 0x00000061,
        CoreN = 0x00000062,
        CoreCountrySpecific = 0x00000063,
        CoreSingleLanguage = 0x00000064,
        Core = 0x00000065,
        ProfessionalWMC = 0x00000067,
        MobileCore = 0x00000068,
        EmbeddedIndustryEval = 0x00000069,
        EmbeddedIndustryEEval = 0x0000006A,
        EmbeddedEval = 0x0000006B,
        EmbeddedEEval = 0x0000006C,
        ServerNano = 0x0000006D,
        ServerCloudStorage = 0x0000006E,
        CoreConnected = 0x0000006F,
        ProfessionalStudent = 0x00000070,
        CoreConnectedN = 0x00000071,
        ProfessionalStudentN = 0x00000072,
        CoreConnectedSingleLanguage = 0x00000073,
        CoreConnectedCountrySpecific = 0x00000074,
        ConnectedCar = 0x00000075,
        IndustryHandheld = 0x00000076,
        PPIPro = 0x00000077,
        ServerARM64 = 0x00000078,
        Education = 0x00000079,
        EducationN = 0x0000007A,
        IoTUAP = 0x0000007B,
        ServerCloudHostInfrastructure = 0x0000007C,
        EnterpriseS = 0x0000007D,
        EnterpriseSN = 0x0000007E,
        ProfessionalS = 0x0000007F,
        ProfessionalSN = 0x00000080,
        EnterpriseSEval = 0x00000081,
        EnterpriseSNEval = 0x00000082,
        IoTUAPCommercial = 0x00000083,
        MobileEnterprise = 0x00000085,
        Holographic = 0x00000087,
        HolographicBusiness = 0x00000088,
        ProfessionalSingleLanguage = 0x0000008A,
        ProfessionalCountrySpecific = 0x0000008B,
        EnterpriseSubscription = 0x0000008C,
        EnterpriseSubscriptionN = 0x0000008D,
        ServerDatacenterNano = 0x0000008F,
        ServerStandardNano = 0x00000090,
        ServerDatacenterACor = 0x00000091,
        ServerStandardACor = 0x00000092,
        ServerDatacenterWSCor = 0x00000093,
        ServerStandardWSCor = 0x00000094,
        UtilityVM = 0x00000095,
        ServerDatacenterEvalCore = 0x0000009F,
        ServerStandardEvalCore = 0x000000A0,
        ProfessionalWorkstation = 0x000000A1,
        ProfessionalWorkstationN = 0x000000A2,
        ProfessionalEducation = 0x000000A4,
        ProfessionalEducationN = 0x000000A5,
        ServerAzureCore = 0x000000A8,
        ServerAzureNano = 0x000000A9,
        EnterpriseG = 0x000000AB,
        EnterpriseGN = 0x000000AC,
        // Business = 0x000000AD,
        // BusinessN = 0x000000AE,
        ServerRdsh = 0x000000AF,
        ServerRdshCore = 0x000000B0,
        Cloud = 0x000000B2,
        CloudN = 0x000000B3,
        HubOS = 0x000000B4,
        OneCoreUpdateOS = 0x000000B6,
        CloudE = 0x000000B7,
        Andromeda = 0x000000B8,
        IoTOS = 0x000000B9,
        CloudEN = 0x000000BA,
        IoTEdgeOS = 0x000000BB,
        IoTEnterprise = 0x000000BC,
        WindowsCore = 0x000000BD,
        IoTEnterpriseS = 0x000000BF,
        XboxSystemOS = 0x000000C0,
        XboxNativeOS = 0x000000C1,
        XboxGameOS = 0x000000C2,
        XboxEraOS = 0x000000C3,
        XboxDurangoHostOS = 0x000000C4,
        XboxScarlettHostOS = 0x000000C5,
        ServerAzureCloudHost = 0x000000C7,
        ServerAzureCloudMOS = 0x000000C8,
        CloudEditionN = 0x000000CA,
        CloudEdition = 0x000000CB,
        ServerAzureStackHCICor = 0x00000196,
        ServerTurbine = 0x00000197,
        ServerTurbineCor = 0x00000198,

    }

    public class PolicyValue
    {
        public string Name;
        public int Type;
        public byte[] Data;
    }

    public class WindowsImageIndex
    {
        public string Name;
        public string Description;
        public string LastModifiedTime;
        public string CreationTime;
        public WindowsImage WindowsImage;
    }

    public class WindowsImage
    {
        public ulong MajorVersion;
        public ulong MinorVersion;
        public ulong BuildNumber;
        public ulong DeltaVersion;
        public string BranchName;
        public string CompileDate;
        public string Tag;
        public MachineType Architecture;
        public BuildType BuildType;
        public HashSet<Type> Types = new HashSet<Type>();
        public string BaseSku;
        public string Sku;
        public string[] Editions;
        public Licensing Licensing;
        public string[] LanguageCodes;
    }

    public enum MachineType : ushort
    {
        unknown = 0x0,
        axp = 0x184,
        am33 = 0x1d3,
        amd64 = 0x8664,
        arm = 0x1c0,
        arm64 = 0xaa64,
        woa = 0x1c4,
        ebc = 0xebc,
        x86 = 0x14c,
        ia64 = 0x200,
        m32r = 0x9041,
        mips16 = 0x266,
        mipsfpu = 0x366,
        mipsfpu16 = 0x466,
        powerpc = 0x1f0,
        powerpcfp = 0x1f1,
        r4000 = 0x166,
        sh3 = 0x1a2,
        sh3dsp = 0x1a3,
        sh4 = 0x1a6,
        sh5 = 0x1a8,
        thumb = 0x1c2,
        wcemipsv2 = 0x169,
        nec98 = 0xffff
    }

    public class WindowsVersion
    {
        public ulong MajorVersion;
        public ulong MinorVersion;
        public ulong BuildNumber;
        public ulong DeltaVersion;
        public string BranchName;
        public string CompileDate;
    }
}
