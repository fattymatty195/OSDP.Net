using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;

namespace OSDP.Net.Model.ReplyData
{
    public class Nak
    {
        private Nak()
        {
        }

        public ErrorCode ErrorCode { get; private set; }

        public IEnumerable<byte> ExtraData { get; private set;  }

        internal static Nak ParseData(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 1)
            {
                throw new Exception("Invalid size for the data");
            }

            var nak = new Nak
            {
                ErrorCode = (ErrorCode)dataArray[0],
                ExtraData = dataArray.Skip(1).Take(dataArray.Length - 1)
            };

            return nak;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var build = new StringBuilder();
            build.AppendLine($"Error: {Message.SplitCamelCase(ErrorCode.ToString())}");
            build.AppendLine($" Data: {BitConverter.ToString(ExtraData.ToArray())}");
            return build.ToString();
        }
    }

    public enum ErrorCode
    {
        NoError = 0x0,
        BadChecksumOrCrc = 0x1,
        InvalidCommandLength = 0x2,
        UnknownCommandCode = 0x3,
        UnexpectedSequenceNumber = 0x4,
        DoesNotSupportSecurityBlock = 0x5,
        CommunicationSecurityNotMet = 0x6,
        BioTypeNotSupported = 0x7,
        BioFormatNotSupported = 0x8,
        UnableToProcessCommand = 0x9,
        GenericError = 0xFF
    }
}