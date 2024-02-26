using System;
using System.Net;
using System.Text;

namespace SimpleChat.Models
{
    /**
     * [Header(2) 0x40 | 0x90 ] [Sender Id(4)] [Sent Time(8)] [Length(4)] [Content(*)]
     */
    public record Message(int SenderId, DateTime Sent, string Content)
    {
        public byte[] Serialize()
        {
            var contentBytes = Encoding.UTF8.GetBytes(Content);
            return [0x40, 0x90, .. BitConverter.GetBytes(SenderId), .. BitConverter.GetBytes(Sent.Ticks), .. BitConverter.GetBytes(contentBytes.Length), .. contentBytes];
        }
    }
}
