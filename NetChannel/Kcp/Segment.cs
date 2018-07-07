//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace NetChannel
//{
//    /// <summary>
//    /// KCP Segment Definition
//    /// KCP分片包定义
//    /// </summary>
//    internal class Segment
//    {
//        internal UInt32 conv;
//        internal UInt32 cmd;
//        internal UInt32 frg;
//        internal UInt32 wnd;
//        internal UInt32 ts;
//        internal UInt32 sn;
//        internal UInt32 una;
//        internal UInt32 resendts;
//        internal UInt32 rto;
//        internal UInt32 fastack;
//        internal UInt32 xmit;
//        internal byte[] data;

//        internal void flush()
//        {
//            conv = 0;
//            cmd = 0;
//            frg = 0;
//            wnd = 0;
//            ts = 0;
//            sn = 0;
//            una = 0;
//            resendts = 0;
//            rto = 0;
//            fastack = 0;
//            xmit = 0;
//        }

//        internal Segment(int size)
//        {
//            this.data = new byte[size];
//        }

//        // encode a segment into buffer
//        internal int encode(byte[] ptr, int offset)
//        {
//            var offset_ = offset;

//            offset += Kcp.ikcp_encode32u(ptr, offset, conv);
//            offset += Kcp.ikcp_encode8u(ptr, offset, (byte)cmd);
//            offset += Kcp.ikcp_encode8u(ptr, offset, (byte)frg);
//            offset += Kcp.ikcp_encode16u(ptr, offset, (UInt16)wnd);
//            offset += Kcp.ikcp_encode32u(ptr, offset, ts);
//            offset += Kcp.ikcp_encode32u(ptr, offset, sn);
//            offset += Kcp.ikcp_encode32u(ptr, offset, una);
//            offset += Kcp.ikcp_encode32u(ptr, offset, (UInt32)data.Length);
//            return offset - offset_;
//        }
//    }
//}
