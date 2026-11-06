using System;
using System.Runtime.InteropServices;

namespace GlassIntro
{
    /// <summary>Общая громкость Windows через Core Audio.</summary>
    internal static class Audio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class DeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int mask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int context, IntPtr parameters,
                [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        }

        private static IAudioEndpointVolume Open()
        {
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new DeviceEnumerator();
            IMMDevice device;
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null) return null;
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object instance;
            if (device.Activate(ref iid, 23, IntPtr.Zero, out instance) != 0) return null;
            return (IAudioEndpointVolume)instance;
        }

        public static double Get()
        {
            try
            {
                IAudioEndpointVolume volume = Open();
                float level;
                if (volume != null && volume.GetMasterVolumeLevelScalar(out level) == 0) return level;
            }
            catch (COMException) { }
            return -1;
        }

        public static void Set(double level)
        {
            try
            {
                IAudioEndpointVolume volume = Open();
                Guid context = Guid.Empty;
                if (volume != null) volume.SetMasterVolumeLevelScalar((float)level, ref context);
            }
            catch (COMException) { }
        }
    }
}
