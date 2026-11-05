using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public enum LengthForm
    {
        Canonical,
        LongForm,
        LongFormLeadingZero
    }

    public sealed class RlpNode
    {
        public bool IsList;
        public byte[] Data = Array.Empty<byte>();
        public List<RlpNode> Items = new List<RlpNode>();
        public byte[] TypePrefix;
        public RlpNode TypedBody;
        public LengthForm Form;

        public static RlpNode Str(byte[] data) => new RlpNode { Data = data };

        public static RlpNode Str(string hex) => new RlpNode { Data = Convert.FromHexString(hex) };

        public static RlpNode Int(ulong value)
        {
            var bytes = new List<byte>();
            while (value != 0)
            {
                bytes.Insert(0, (byte)(value & 0xff));
                value >>= 8;
            }
            return new RlpNode { Data = bytes.ToArray() };
        }

        public static RlpNode List(params RlpNode[] items) => new RlpNode { IsList = true, Items = items.ToList() };

        public static RlpNode List(IEnumerable<RlpNode> items) => new RlpNode { IsList = true, Items = items.ToList() };

        public static RlpNode Typed(byte type, RlpNode body) => new RlpNode { TypePrefix = new[] { type }, TypedBody = body };

        public bool IsTyped => TypePrefix != null;

        public RlpNode Clone()
        {
            return new RlpNode
            {
                IsList = IsList,
                Data = Data,
                Items = Items.Select(i => i.Clone()).ToList(),
                TypePrefix = TypePrefix,
                TypedBody = TypedBody?.Clone(),
                Form = Form
            };
        }

        public IEnumerable<RlpNode> Descendants()
        {
            yield return this;
            if (IsTyped)
            {
                foreach (var d in TypedBody.Descendants()) yield return d;
            }
            else
            {
                foreach (var item in Items)
                    foreach (var d in item.Descendants()) yield return d;
            }
        }

        public byte[] Encode()
        {
            using (var stream = new MemoryStream())
            {
                Encode(stream);
                return stream.ToArray();
            }
        }

        private void Encode(MemoryStream stream)
        {
            if (IsTyped)
            {
                var content = TypePrefix.Concat(TypedBody.Encode()).ToArray();
                WriteString(stream, content);
                return;
            }

            if (!IsList)
            {
                WriteString(stream, Data);
                return;
            }

            using (var body = new MemoryStream())
            {
                foreach (var item in Items) item.Encode(body);
                WriteHeader(stream, 0xc0, (int)body.Length, Form);
                body.WriteTo(stream);
            }
        }

        private void WriteString(MemoryStream stream, byte[] content)
        {
            if (content.Length == 1 && content[0] < 0x80 && Form == LengthForm.Canonical)
            {
                stream.WriteByte(content[0]);
                return;
            }
            WriteHeader(stream, 0x80, content.Length, Form);
            stream.Write(content, 0, content.Length);
        }

        private static void WriteHeader(MemoryStream stream, int shortBase, int length, LengthForm form)
        {
            if (form == LengthForm.Canonical && length <= 55)
            {
                stream.WriteByte((byte)(shortBase + length));
                return;
            }

            var lengthBytes = new List<byte>();
            var value = length;
            while (value != 0)
            {
                lengthBytes.Insert(0, (byte)(value & 0xff));
                value >>= 8;
            }
            if (lengthBytes.Count == 0) lengthBytes.Add(0);
            if (form == LengthForm.LongFormLeadingZero) lengthBytes.Insert(0, 0);
            stream.WriteByte((byte)(shortBase + 55 + lengthBytes.Count));
            stream.Write(lengthBytes.ToArray(), 0, lengthBytes.Count);
        }

        public byte[] EncodeBinary()
        {
            return IsTyped ? TypePrefix.Concat(TypedBody.Encode()).ToArray() : Encode();
        }

        public static List<int> PreorderStarts(RlpNode root, bool binary = false)
        {
            var offsets = new List<int>();
            if (binary && root.IsTyped)
            {
                offsets.Add(0);
                Collect(root.TypedBody, root.TypePrefix.Length, offsets);
                return offsets;
            }
            Collect(root, 0, offsets);
            return offsets;
        }

        private static int Collect(RlpNode node, int start, List<int> offsets)
        {
            offsets.Add(start);
            var encoded = node.Encode();
            if (node.IsTyped)
            {
                var bodyLength = node.TypedBody.Encode().Length;
                Collect(node.TypedBody, start + encoded.Length - bodyLength, offsets);
            }
            else if (node.IsList)
            {
                var itemsLength = 0;
                var lengths = new List<int>();
                foreach (var item in node.Items)
                {
                    var length = item.Encode().Length;
                    lengths.Add(length);
                    itemsLength += length;
                }
                var cursor = start + encoded.Length - itemsLength;
                for (var i = 0; i < node.Items.Count; i++)
                {
                    Collect(node.Items[i], cursor, offsets);
                    cursor += lengths[i];
                }
            }
            return encoded.Length;
        }

        public static bool TryParse(byte[] bytes, out RlpNode node)
        {
            node = null;
            try
            {
                var position = 0;
                var parsed = Parse(bytes, ref position, bytes.Length, 0);
                if (position != bytes.Length) return false;
                node = parsed;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static RlpNode Parse(byte[] bytes, ref int position, int end, int depth)
        {
            if (depth > 64 || position >= end) throw new FormatException();
            var first = bytes[position];
            if (first < 0x80)
            {
                position++;
                return new RlpNode { Data = new[] { first } };
            }

            int headerLength, length;
            var form = LengthForm.Canonical;
            bool isList = first >= 0xc0;
            var shortBase = isList ? 0xc0 : 0x80;
            if (first <= shortBase + 55)
            {
                headerLength = 1;
                length = first - shortBase;
            }
            else
            {
                var lengthOfLength = first - shortBase - 55;
                if (position + 1 + lengthOfLength > end) throw new FormatException();
                long value = 0;
                for (var i = 0; i < lengthOfLength; i++) value = (value << 8) | bytes[position + 1 + i];
                if (value > int.MaxValue) throw new FormatException();
                length = (int)value;
                headerLength = 1 + lengthOfLength;
                form = bytes[position + 1] == 0 ? LengthForm.LongFormLeadingZero : LengthForm.LongForm;
            }

            if (position + headerLength + length > end) throw new FormatException();
            var contentStart = position + headerLength;
            position = contentStart + length;
            if (!isList)
            {
                var data = new byte[length];
                Array.Copy(bytes, contentStart, data, 0, length);
                return new RlpNode { Data = data, Form = form };
            }

            var node = new RlpNode { IsList = true, Form = form };
            var cursor = contentStart;
            while (cursor < contentStart + length)
                node.Items.Add(Parse(bytes, ref cursor, contentStart + length, depth + 1));
            return node;
        }
    }
}
