using System;
using System.Collections.Generic;
using System.Text;

namespace NetChannel
{
    /// <summary>
    /// KCP Segment Definition
    /// KCP分片包定义
    /// </summary>
    internal class Segment : IDisposable
    {
        internal uint conv;
        internal uint cmd;
        internal uint frg;
        internal uint wnd;
        internal uint ts;
        internal uint sn;
        internal uint una;
        internal uint resendts;
        internal uint rto;
        internal uint faskack;
        internal uint xmit;
        internal byte[] data { get; }

        internal Segment(int size = 0)
        {
            data = new byte[size];
        }

        internal void Encode(byte[] ptr, ref int offset)
        {
            uint len = (uint)data.Length;
            Kcp.ikcp_encode32u(ptr, offset, conv);
            Kcp.ikcp_encode8u(ptr, offset + 4, (byte)cmd);
            Kcp.ikcp_encode8u(ptr, offset + 5, (byte)frg);
            Kcp.ikcp_encode16u(ptr, offset + 6, (ushort)wnd);
            Kcp.ikcp_encode32u(ptr, offset + 8, ts);
            Kcp.ikcp_encode32u(ptr, offset + 12, sn);
            Kcp.ikcp_encode32u(ptr, offset + 16, una);
            Kcp.ikcp_encode32u(ptr, offset + 20, len);
            offset += Kcp.IKCP_OVERHEAD;
        }

        public void Dispose()
        {
            this.conv = 0;
            this.cmd = 0;
            this.frg = 0;
            this.wnd = 0;
            this.ts = 0;
            this.sn = 0;
            this.una = 0;
            this.resendts = 0;
            this.rto = 0;
            this.faskack = 0;
            this.xmit = 0;
        }
    }
}
