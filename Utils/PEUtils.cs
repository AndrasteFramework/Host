using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Andraste.Host.Utils
{
    public class PEUtils
    {
        private static readonly byte[] PE_MAGIC = { 0x50, 0x45, 0x0, 0x0 };
        private const int COFF_HEADER_POINTER = 0x3C;
        private const int COFF_OFFSET_MACHINE = 4;
        private const short MACHINE_TYPE_I386 = 0x14C;
        private const int COFF_OFFSET_CHARACTERISTICS = 22;
        public const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020;
        
        private int _coffHeaderOffset = -1;
        private readonly Stream _stream;

        public PEUtils(Stream stream)
        {
            // We need this ugly non-static class so we can cache offsets to reduce useless seeks.
            _stream = stream;
        }

        public async Task<ushort> GetCharacteristics()
        {
            await SeekUntilCOFF();
            _stream.Seek(COFF_OFFSET_CHARACTERISTICS, SeekOrigin.Current);
            return BitConverter.ToUInt16(await ReadExactAsync(_stream, 2), 0);
        }
        
        public async Task SetCharacteristics(ushort characteristics)
        {
            await SeekUntilCOFF();
            _stream.Seek(COFF_OFFSET_CHARACTERISTICS, SeekOrigin.Current);
            await _stream.WriteAsync(BitConverter.GetBytes(characteristics), 0, 2);
        }
        
        public async Task<bool> Is32Bit()
        {
            await SeekUntilCOFF();
            _stream.Seek(COFF_OFFSET_MACHINE, SeekOrigin.Current);
            return MACHINE_TYPE_I386 == BitConverter.ToUInt16(await ReadExactAsync(_stream, 2), 0);
        }

        public async Task SetLargeAddressAware(bool set)
        {
            if (!await Is32Bit())
            {
                return;
            }
            
            var characteristics = await GetCharacteristics();
            
            if (set)
            {
                characteristics |= IMAGE_FILE_LARGE_ADDRESS_AWARE;
            }
            else
            {
                characteristics &= unchecked((ushort)~IMAGE_FILE_LARGE_ADDRESS_AWARE);
            }

            await SetCharacteristics(characteristics);
        }

        private async Task SeekUntilCOFF(bool validateMagic = true)
        {
            if (_coffHeaderOffset != -1)
            {
                _stream.Seek(_coffHeaderOffset, SeekOrigin.Begin);
                return;
            }
            
            _stream.Seek(COFF_HEADER_POINTER, SeekOrigin.Begin);
            _coffHeaderOffset = BitConverter.ToInt32(await ReadExactAsync(_stream, 4), 0);
            _stream.Seek(_coffHeaderOffset, SeekOrigin.Begin);

            if (validateMagic)
            {
                var magic = await ReadExactAsync(_stream, 4);
                if (!magic.SequenceEqual(PE_MAGIC))
                {
                    throw new InvalidDataException("Not a valid PE file");
                }
                _stream.Seek(_coffHeaderOffset, SeekOrigin.Begin);
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