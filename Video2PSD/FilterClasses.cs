using System;
using System.Runtime.InteropServices;

namespace Video2PSD
{
    /// <summary>
    /// CLSID_LAVSplitter
    /// </summary>
    [ComImport, Guid("171252A0-8820-4AFE-9DF8-5C92B2D66B04")]
    public class LAVSplitter { }

    /// <summary>
    /// CLSID_LAVVideoDecoder
    /// </summary>
    [ComImport, Guid("EE30215D-164F-4A92-A4EB-9D4C13390F9F")]
    public class LAVVideoDecoder { }

    /// <summary>
    /// CLSID_LAVAudioDecoder
    /// </summary>
    [ComImport, Guid("E8E73B6B-4CB3-44A4-BE99-4F7BCB96E491")]
    public class LAVAudioDecoder { }

    /// <summary>
    /// CLSID_DirectVobSub
    /// </summary>
    [ComImport, Guid("9852A670-F845-491B-9BE6-EBD841B8A613")]
    public class DirectVobSub { }

    /// <summary>
    /// CLSID_madVR
    /// </summary>
    [ComImport, Guid("E1A8B82A-32CE-4B0D-BE0D-AA68C772E423")]
    public class madVR { }
}