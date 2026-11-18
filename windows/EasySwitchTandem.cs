using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

[assembly: AssemblyTitle("Logitech Easy-Switch Tandem Utility")]
[assembly: AssemblyDescription("Logitech HID++ ChangeHost (0x1814) Easy-Switch Utility")]
[assembly: AssemblyCompany("LogiSwitch Local Tooling")]
[assembly: AssemblyProduct("EasySwitch-Tandem")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]

namespace EasySwitch {
    public class Hid {
        const int  DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
        const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_RW = 0x03, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
        const int  HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        struct GUID { public uint a; public ushort b, c; [MarshalAs(UnmanagedType.ByValArray, SizeConst=8)] public byte[] d; }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public GUID guid; public int flags; public IntPtr reserved; }

        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA_W {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=512)] public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES { public int Size; public ushort Vid, Pid, Ver; }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS {
            public ushort Usage, UsagePage, InputLen, OutputLen, FeatureLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
            public ushort LinkNodes, InBtn, InVal, InIdx, OutBtn, OutVal, OutIdx, FtBtn, FtVal, FtIdx;
        }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(ref GUID g);
        [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
        [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr pp);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr pp);
        [DllImport("hid.dll")] static extern int  HidP_GetCaps(IntPtr pp, ref HIDP_CAPS c);
        [DllImport("hid.dll", CharSet=CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle h, char[] buf, int len);
        [DllImport("hid.dll")] static extern bool HidD_SetOutputReport(SafeFileHandle h, byte[] buf, int len);

        [DllImport("setupapi.dll", CharSet=CharSet.Unicode)]
        static extern IntPtr SetupDiGetClassDevsW(ref GUID g, IntPtr e, IntPtr w, int f);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref GUID g, int i, ref SP_DEVICE_INTERFACE_DATA a);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr s, ref SP_DEVICE_INTERFACE_DATA a, ref SP_DEVICE_INTERFACE_DETAIL_DATA_W d, int size, IntPtr req, IntPtr info);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);

        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern bool CancelIoEx(SafeFileHandle h, IntPtr overlapped);

        public class Iface {
            public string Path, Product;
            public ushort UsagePage, Usage, Pid, InLen, OutLen;
        }

        public static List<Iface> Enumerate(ushort vid) {
            var result = new List<Iface>();
            var guid = new GUID();
            HidD_GetHidGuid(ref guid);
            IntPtr set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return result;
            try {
                var did = new SP_DEVICE_INTERFACE_DATA();
                did.cbSize = Marshal.SizeOf(did);
                for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref did); i++) {
                    var detail = new SP_DEVICE_INTERFACE_DETAIL_DATA_W();
                    detail.cbSize = IntPtr.Size == 8 ? 8 : 6;
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref did, ref detail, Marshal.SizeOf(detail), IntPtr.Zero, IntPtr.Zero))
                        continue;

                    using (var h = CreateFileW(detail.DevicePath, 0, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero)) {
                        if (h.IsInvalid) continue;
                        var attrs = new HIDD_ATTRIBUTES(); attrs.Size = Marshal.SizeOf(attrs);
                        if (!HidD_GetAttributes(h, ref attrs) || attrs.Vid != vid) continue;

                        IntPtr pp;
                        if (!HidD_GetPreparsedData(h, out pp)) continue;
                        var caps = new HIDP_CAPS();
                        int ok = HidP_GetCaps(pp, ref caps);
                        HidD_FreePreparsedData(pp);
                        if (ok != HIDP_STATUS_SUCCESS) continue;
                        if (caps.UsagePage < 0xFF00) continue;
                        if (caps.OutputLen == 0) continue;

                        var name = new char[128];
                        HidD_GetProductString(h, name, 256);
                        result.Add(new Iface {
                            Path = detail.DevicePath, Product = new string(name).TrimEnd('\0'),
                            UsagePage = caps.UsagePage, Usage = caps.Usage, Pid = attrs.Pid,
                            InLen = caps.InputLen, OutLen = caps.OutputLen
                        });
                    }
                }
            } finally { SetupDiDestroyDeviceInfoList(set); }
            return result;
        }

        FileStream stream;
        SafeFileHandle handle;
        int inLen, outLen;
        public int OutLen { get { return outLen; } }

        public Hid(Iface iface) {
            var h = CreateFileW(iface.Path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h.IsInvalid) throw new IOException("cannot open " + iface.Path);
            inLen = iface.InLen; outLen = iface.OutLen;
            try {
                stream = new FileStream(h, FileAccess.ReadWrite, 1, true);
            } catch {
                h.Dispose();
                throw;
            }
            handle = h;
        }

        public bool Write(byte[] frame) {
            if (outLen <= 0 || frame == null || frame.Length == 0) return false;
            var buf = new byte[outLen];
            Array.Copy(frame, buf, Math.Min(frame.Length, outLen));
            try {
                stream.Write(buf, 0, outLen);
                stream.Flush();
                return true;
            } catch {
                try {
                    return HidD_SetOutputReport(handle, frame, frame.Length);
                } catch { return false; }
            }
        }

        public byte[] Read(int timeoutMs) {
            if (inLen <= 0) return new byte[0];
            var buf = new byte[inLen];
            try {
                var task = stream.ReadAsync(buf, 0, inLen);
                if (Task.WaitAny(new Task[] { task }, timeoutMs) != 0) {
                    CancelIoEx(handle, IntPtr.Zero);
                    try { task.Wait(300); } catch { }
                    return new byte[0];
                }
                if (task.IsFaulted || task.Result <= 0) return new byte[0];
                var outb = new byte[task.Result];
                Array.Copy(buf, outb, task.Result);
                return outb;
            } catch { return new byte[0]; }
        }

        public void Close() { try { stream.Dispose(); } catch { } }
    }

    public class Program {
        const ushort LOGITECH_VID = 0x046D;
        static byte nextSwId = 0x0A;

        class TargetDevice {
            public Hid Dev;
            public string IfacePath;
            public ushort InLen;
            public ushort OutLen;
            public byte ReportId;
            public byte DevIdx;
            public byte FeatureIdx;
            public string Name;
            public string Identity;
            public int Rank;
        }

        static int DeviceRank(string name) {
            string n = (name ?? "").ToLowerInvariant();
            bool mouse = n.Contains("m750") || n.Contains("signature") || n.Contains("master") || n.Contains("anywhere") || n.Contains("mouse") || n.Contains("ergo");
            bool kbd = n.Contains("keys") || n.Contains("keyboard");
            return (kbd && !mouse) ? 20 : (mouse ? 0 : 10);
        }

        static byte[] HidppCall(Hid dev, byte reportId, byte devIdx, byte featureIdx, byte function, byte[] parms, int timeoutMs) {
            int flen = reportId == 0x10 ? 7 : 20;
            byte[] frame = new byte[flen];
            byte swId = nextSwId;
            nextSwId = (byte)(8 + ((swId - 8 + 1) % 8));
            frame[0] = reportId;
            frame[1] = devIdx;
            frame[2] = featureIdx;
            frame[3] = (byte)((function << 4) | swId);
            if (parms != null) Array.Copy(parms, 0, frame, 4, Math.Min(parms.Length, flen - 4));
            if (!dev.Write(frame)) return null;
            if (timeoutMs <= 0) return frame;

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline) {
                byte[] r = dev.Read(60);
                if (r == null || r.Length < 5) continue;
                if (r[0] != 0x10 && r[0] != 0x11) continue;
                int rlen = r[0] == 0x10 ? 7 : 20;
                if (r.Length < rlen) continue;
                if (r[1] != devIdx && !(devIdx == 0xFF && r[1] == 0)) continue;
                byte hdr = (byte)((function << 4) | swId);
                if (r[2] == 0xFF || r[2] == 0x8F) {
                    if (r.Length >= 6 && r[3] == featureIdx && r[4] == hdr) return null;
                    continue;
                }
                if (r[2] == featureIdx && r[3] == hdr) return r;
            }
            return null;
        }

        static bool ProbeBoltSlot(Hid shortDev, Hid longDev, byte slot, out string name, out string identity, out byte fallbackFeat) {
            name = ""; identity = ""; fallbackFeat = 0;
            if (shortDev == null) return true;
            byte[] reqInfo = new byte[] { 0x10, 0xFF, 0x83, 0xB5, (byte)(0x50 + slot), 0x00, 0x00 };
            if (!shortDev.Write(reqInfo)) return true;
            DateTime dl = DateTime.UtcNow.AddMilliseconds(180);
            int paired = -1;
            while (DateTime.UtcNow < dl) {
                byte[] rShort = shortDev.Read(30);
                if (rShort != null && rShort.Length >= 7) {
                    if (rShort[1] == 0xFF && rShort[2] == 0x8F && rShort[3] == 0x83 && rShort[4] == 0xB5) {
                        paired = (rShort[5] == 0x08 || rShort[5] == 0x09) ? 0 : 1;
                        break;
                    }
                    if (rShort[1] == 0xFF && rShort[2] == 0x83 && rShort[3] == 0xB5 && rShort[4] == (byte)(0x50 + slot)) {
                        paired = 1;
                        if (rShort.Length >= 10)
                            identity = string.Format("unit:{0:x2}{1:x2}{2:x2}{3:x2}", rShort[6], rShort[7], rShort[8], rShort[9]);
                        break;
                    }
                }
                if (longDev != null) {
                    byte[] rLong = longDev.Read(30);
                    if (rLong != null && rLong.Length >= 10 && rLong[1] == 0xFF && rLong[2] == 0x83 && rLong[3] == 0xB5 && rLong[4] == (byte)(0x50 + slot)) {
                        paired = 1;
                        identity = string.Format("unit:{0:x2}{1:x2}{2:x2}{3:x2}", rLong[6], rLong[7], rLong[8], rLong[9]);
                        break;
                    }
                }
            }
            if (paired == 0) return false;
            if (paired < 0) return true;

            byte[] reqName = new byte[] { 0x10, 0xFF, 0x83, 0xB5, (byte)(0x60 + slot), 0x01, 0x00 };
            if (shortDev.Write(reqName)) {
                dl = DateTime.UtcNow.AddMilliseconds(180);
                while (DateTime.UtcNow < dl) {
                    if (longDev != null) {
                        byte[] rLong = longDev.Read(30);
                        if (rLong != null && rLong.Length >= 8 && rLong[1] == 0xFF && rLong[2] == 0x83 && rLong[3] == 0xB5 && rLong[4] == (byte)(0x60 + slot)) {
                            int nlen = Math.Min((int)rLong[6], rLong.Length - 7);
                            if (nlen > 0) name = Encoding.UTF8.GetString(rLong, 7, nlen).TrimEnd('\0');
                            break;
                        }
                    }
                    byte[] rShort = shortDev.Read(30);
                    if (rShort != null && rShort.Length >= 7) {
                        if (rShort[1] == 0xFF && rShort[2] == 0x8F && rShort[3] == 0x83 && rShort[4] == 0xB5) break;
                        if (rShort.Length >= 8 && rShort[1] == 0xFF && rShort[2] == 0x83 && rShort[3] == 0xB5 && rShort[4] == (byte)(0x60 + slot)) {
                            int nlen = Math.Min((int)rShort[6], rShort.Length - 7);
                            if (nlen > 0) name = Encoding.UTF8.GetString(rShort, 7, nlen).TrimEnd('\0');
                            break;
                        }
                    }
                }
            }
            string upper = name.ToUpperInvariant();
            if (upper.Contains("M750") || upper.Contains("MX KEYS")) fallbackFeat = 0x0A;
            return true;
        }

        static string CacheFilePath() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"mxswitch\tandem_cache.txt");
        }

        static List<TargetDevice> LoadCachedTargets(List<Hid> opened) {
            var list = new List<TargetDevice>();
            string path = CacheFilePath();
            if (!File.Exists(path)) return list;
            try {
                var byPath = new Dictionary<string, Hid>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(path)) {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] p = line.Split('|');
                    if (p.Length < 6) continue;
                    string devPath = p[0];
                    ushort inLen = ushort.Parse(p[1]);
                    ushort outLen = ushort.Parse(p[2]);
                    byte devIdx = byte.Parse(p[3]);
                    byte featIdx = byte.Parse(p[4]);
                    string name = p[5];

                    Hid dev;
                    if (!byPath.TryGetValue(devPath, out dev)) {
                        var iface = new Hid.Iface { Path = devPath, InLen = inLen, OutLen = outLen, Product = name, UsagePage = 0xFF00 };
                        dev = new Hid(iface);
                        byPath[devPath] = dev;
                        opened.Add(dev);
                    }
                    list.Add(new TargetDevice {
                        Dev = dev, IfacePath = devPath, InLen = inLen, OutLen = outLen,
                        ReportId = 0x11, DevIdx = devIdx, FeatureIdx = featIdx,
                        Name = name, Identity = devPath + ":" + devIdx, Rank = DeviceRank(name)
                    });
                }
            } catch {
                list.Clear();
            }
            return list;
        }

        static void SaveCachedTargets(List<TargetDevice> targets) {
            try {
                string path = CacheFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new List<string>();
                foreach (var t in targets) {
                    lines.Add(string.Format("{0}|{1}|{2}|{3}|{4}|{5}", t.IfacePath, t.InLen, t.OutLen, t.DevIdx, t.FeatureIdx, t.Name));
                }
                File.WriteAllLines(path, lines.ToArray());
            } catch { }
        }

        static void SwitchDeviceVerified(TargetDevice t, byte hostByte) {
            for (int attempt = 0; attempt < 5; attempt++) {
                HidppCall(t.Dev, t.ReportId, t.DevIdx, t.FeatureIdx, 1, new byte[] { hostByte }, 0);
                Thread.Sleep(150);
                // Verify by absence or by updated channel: if the device still replies on the old channel, resend!
                byte[] still = HidppCall(t.Dev, t.ReportId, t.DevIdx, t.FeatureIdx, 0, null, 160);
                if (still == null || (still.Length >= 6 && still[5] == hostByte))
                    break;
            }
        }

        static void LoadConfig(out int channelA, out int channelB) {
            channelA = 1; channelB = 2;
            string cfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"mxswitch\config");
            if (!File.Exists(cfgPath)) return;
            foreach (string raw in File.ReadAllLines(cfgPath)) {
                string line = raw.Split('#')[0].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split(new char[] { '=' }, 2);
                if (parts.Length < 2) continue;
                string k = parts[0].Trim().ToLowerInvariant();
                int v;
                if (int.TryParse(parts[1].Trim(), out v) && v >= 1 && v <= 3) {
                    if (k == "channel_a") channelA = v;
                    else if (k == "channel_b") channelB = v;
                }
            }
        }

        [STAThread]
        public static int Main(string[] args) {
            int channelA, channelB;
            LoadConfig(out channelA, out channelB);

            bool dryRun = false;
            int explicitChannel = 0;
            foreach (string a in args) {
                if (a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase) || a.Equals("--cache", StringComparison.OrdinalIgnoreCase))
                    dryRun = true;
                else if (int.TryParse(a, out explicitChannel) && (explicitChannel < 1 || explicitChannel > 3))
                    explicitChannel = 0;
            }

            var opened = new List<Hid>();
            try {
                var targets = LoadCachedTargets(opened);
                if (targets.Count < 2) {
                    foreach (var d in opened) d.Close();
                    opened.Clear();
                    targets.Clear();
                    var ifaces = Hid.Enumerate(LOGITECH_VID);
                    var shortDevs = new Dictionary<string, Hid>(StringComparer.OrdinalIgnoreCase);
                    foreach (var sIface in ifaces) {
                        if (sIface.OutLen == 7) {
                            try {
                                string baseKey = System.Text.RegularExpressions.Regex.Replace(sIface.Path.ToLowerInvariant(), @"&col[0-9a-f]{2}", "");
                                var sDev = new Hid(sIface);
                                opened.Add(sDev);
                                shortDevs[baseKey] = sDev;
                            } catch { }
                        }
                    }

                    foreach (var iface in ifaces) {
                        if (iface.OutLen != 20) continue; // Only probe the 20-byte HID++ 2.0 long report collection (0x11)
                        Hid dev;
                        try { dev = new Hid(iface); } catch { continue; }
                        opened.Add(dev);

                        string baseKey = System.Text.RegularExpressions.Regex.Replace(iface.Path.ToLowerInvariant(), @"&col[0-9a-f]{2}", "");
                        Hid shortDev = null;
                        shortDevs.TryGetValue(baseKey, out shortDev);

                        byte[] indices = new byte[] { 1, 2, 3, 4, 5, 6, 0xFF };
                        foreach (byte didx in indices) {
                            if (didx == 0xFF && (iface.Product ?? "").IndexOf("Receiver", StringComparison.OrdinalIgnoreCase) >= 0)
                                continue;
                            string sName = "", sId = "";
                            byte fbFeat = 0;
                            if (didx != 0xFF) {
                                if (!ProbeBoltSlot(shortDev, dev, didx, out sName, out sId, out fbFeat)) continue;
                            }
                            byte featIdx = fbFeat;
                            if (featIdx == 0) {
                                byte[] r = HidppCall(dev, 0x11, didx, 0x00, 0, new byte[] { 0x18, 0x14, 0x00 }, 850);
                                if (r != null) featIdx = r[4];
                            }
                            if (featIdx == 0) continue;

                            string devName = !string.IsNullOrEmpty(sName) ? sName : (!string.IsNullOrEmpty(iface.Product) ? iface.Product : ("Slot " + didx));
                            string devId = !string.IsNullOrEmpty(sId) ? sId : (iface.Path.ToLowerInvariant() + ":" + didx);

                            var td = new TargetDevice {
                                Dev = dev, IfacePath = iface.Path, InLen = iface.InLen, OutLen = iface.OutLen,
                                ReportId = 0x11, DevIdx = didx, FeatureIdx = featIdx,
                                Name = devName, Identity = devId, Rank = DeviceRank(devName)
                            };

                            int existing = targets.FindIndex(t => t.Identity == devId);
                            if (existing >= 0) targets[existing] = td;
                            else targets.Add(td);

                            if (didx == 0xFF) break;
                        }
                    }
                    if (targets.Count > 0) {
                        targets.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                        SaveCachedTargets(targets);
                    }
                }

                if (targets.Count == 0) return 1;
                targets.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                if (dryRun) return 0;

                int targetChannel = explicitChannel;
                if (targetChannel == 0) {
                    int current = 0;
                    foreach (var t in targets) {
                        byte[] r = HidppCall(t.Dev, t.ReportId, t.DevIdx, t.FeatureIdx, 0, null, 120);
                        if (r != null && r.Length >= 6) {
                            current = r[5] + 1;
                            break;
                        }
                    }
                    if (current == 0) current = channelB; // On secondary Windows machine (Channel 2), default current to channelB so toggle switches to channelA (1)
                    targetChannel = (current == channelA) ? channelB : channelA;
                }

                byte hostByte = (byte)(targetChannel - 1);
                // Fire setCurrentHost to all targets immediately in parallel
                foreach (var t in targets) {
                    HidppCall(t.Dev, t.ReportId, t.DevIdx, t.FeatureIdx, 1, new byte[] { hostByte }, 0);
                }
                Thread.Sleep(120);
                foreach (var t in targets) {
                    HidppCall(t.Dev, t.ReportId, t.DevIdx, t.FeatureIdx, 1, new byte[] { hostByte }, 0);
                }
                return 0;
            } finally {
                foreach (var d in opened) d.Close();
            }
        }
    }
}
