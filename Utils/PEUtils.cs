using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Andraste.Host.Utils
{
    public static class PEUtils
    {
        private const int COFF_HEADER_POINTER = 0x3C;
        private static readonly byte[] PE_MAGIC = { 0x50, 0x45, 0x0, 0x0 };
        private const int COFF_OFFSET_MACHINE = 4;
        private const short MACHINE_TYPE_I386 = 0x14C;
        private const int COFF_OFFSET_CHARACTERISTICS = 22;
        public const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020;

        public static async Task<ushort> GetCharacteristics(Stream stream)
        {
            await SeekUntilCOFF(stream);
            stream.Seek(COFF_OFFSET_CHARACTERISTICS, SeekOrigin.Current);            
            var pos = stream.Position;
            return BitConverter.ToUInt16(await ReadExactAsync(stream, 2), 0);
        }
        
        public static async Task SetCharacteristics(Stream stream, ushort characteristics)
        {
            await SeekUntilCOFF(stream);
            stream.Seek(COFF_OFFSET_CHARACTERISTICS, SeekOrigin.Current);
            await stream.WriteAsync(BitConverter.GetBytes(characteristics), 0, 2);
        }
        
        public static async Task<bool> Is32Bit(Stream stream)
        {
            await SeekUntilCOFF(stream);
            stream.Seek(COFF_OFFSET_MACHINE, SeekOrigin.Current);
            return MACHINE_TYPE_I386 == BitConverter.ToUInt16(await ReadExactAsync(stream, 2), 0);
        }

        public static async Task SetLargeAddressAware(Stream stream)
        {
            // TODO: with some refactoring, this could prevent some seeking, but then we don't need too much performance here
            if (!await Is32Bit(stream))
            {
                return;
            }
            
            var characteristics = await GetCharacteristics(stream);
            characteristics |= IMAGE_FILE_LARGE_ADDRESS_AWARE;
            await SetCharacteristics(stream, characteristics);
        }

        private static async Task SeekUntilCOFF(Stream stream, bool validateMagic = true)
        {
            stream.Seek(COFF_HEADER_POINTER, SeekOrigin.Begin);
            var offset = BitConverter.ToInt32(await ReadExactAsync(stream, 4), 0);
            stream.Seek(offset, SeekOrigin.Begin);

            if (validateMagic)
            {
                var magic = await ReadExactAsync(stream, 4);
                if (!magic.SequenceEqual(PE_MAGIC))
                {
                    throw new InvalidDataException("Not a valid PE file");
                }
                stream.Seek(offset, SeekOrigin.Begin);
            }
        }
        
        private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
        {
            var buffer = new byte[count];
            var bytesRead = 0;
            while (bytesRead < count)
            {
                var newBytesRead = await stream.ReadAsync(buffer, bytesRead, count - bytesRead);
                if (newBytesRead == 0)
                {
                    throw new EndOfStreamException();
                }
                
                bytesRead += newBytesRead;
            }
            return buffer;
        }
    }
}