using System;
using System.Net;
using System.Text;

namespace SimpleChat.Models
{
    /**
    * [Header(2) 0x40 | 0x90 ] [Sender IPv4 Address(4)] [Length(4)] [Content(*)]
    * TODO: [Header(2) 0x40 | 0x90 ] [Sender IPv4 Address(4)] [Send Time(8)] [Length(4)] [Content(*)]
    */
    public record Message(IPAddress Sender, string Content)
    {
        public byte[] Serialize()
        {
            var contentBytes = Encoding.UTF8.GetBytes(Content);
            return [0x40, 0x90, .. Sender.GetAddressBytes(), .. BitConverter.GetBytes(contentBytes.Length), .. contentBytes];
        }
    }
}
