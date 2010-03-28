﻿/* This file is part of libWiiSharp
 * Copyright (C) 2009 Leathl
 * 
 * libWiiSharp is free software: you can redistribute it and/or
 * modify it under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * libWiiSharp is distributed in the hope that it will be
 * useful, but WITHOUT ANY WARRANTY; without even the implied warranty
 * of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;

namespace libWiiSharp
{
    /// <summary>
    /// A class for RIFF Wave files
    /// </summary>
    public class Wave
    {
        //Private Variables
        private int fmtOffset;
        private int dataOffset;
        private int smplOffset;
        private byte[] waveFile;



        //Public Variables
        /// <summary>
        /// The Samplerate of the Wave file
        /// </summary>
        public int SampleRate { get { return GetSampleRate(); } }
        /// <summary>
        /// The Bitdepth of the Wave file
        /// </summary>
        public int BitDepth { get { return GetBitDepth(); } }
        /// <summary>
        /// The number of channels of the Wave file
        /// </summary>
        public int ChannelCount { get { return GetChannelCount(); } }
        /// <summary>
        /// The format of the Wave file's data. 1 = PCM
        /// </summary>
        public int DataFormat { get { return GetDataFormat(); } }
        /// <summary>
        /// The number of Loops in the Wave file
        /// </summary>
        public int LoopCount { get { return GetLoopCount(); } }
        /// <summary>
        /// The start sample of the first Loop (if exist)
        /// </summary>
        public int LoopStart { get { return GetLoopStart(); } }
        /// <summary>
        /// The total number of Frames
        /// </summary>
        public int SampleCount { get { return GetFrameCount(); } }



        //Public Functions

        public Wave(string waveFile)
        {
            using (FileStream fs = new FileStream(waveFile, FileMode.Open))
            {
                byte[] temp = new byte[fs.Length];
                fs.Read(temp, 0, temp.Length);

                this.waveFile = temp;
            }

            if (!CheckWave()) throw new Exception("This is not a supported PCM Wave File!");

            if (!GetFmtOffset()) throw new Exception("The format section couldn't be found!");
            if (!GetDataOffset()) throw new Exception("The data section couldn't be found!");
            GetSmplOffset();
        }

        public Wave(byte[] waveFile)
        {
            this.waveFile = waveFile;

            if (!CheckWave()) throw new Exception("This is not a supported PCM Wave File!");

            if (!GetFmtOffset()) throw new Exception("The format section couldn't be found!");
            if (!GetDataOffset()) throw new Exception("The data section couldn't be found!");
            GetSmplOffset();
        }

        /// <summary>
        /// Returns the playlength of the Wave file in seconds
        /// </summary>
        /// <param name="wavefile"></param>
        /// <returns></returns>
        public int GetWaveLength()
        {
            int bps = BitConverter.ToInt32(waveFile, 28);
            int chunks = BitConverter.ToInt32(waveFile, 4);

            return Math.Abs(chunks / bps);
        }

        /// <summary>
        /// Returns the Wave file as a Byte Array
        /// </summary>
        /// <returns></returns>
        public byte[] ToByteArray()
        {
            return waveFile;
        }

        /// <summary>
        /// Returns the Wave file as a Memory Stream
        /// </summary>
        /// <returns></returns>
        public MemoryStream ToMemoryStream()
        {
            return new MemoryStream(waveFile);
        }

        /// <summary>
        /// Returns only the audio data of the Wave file (No header or anything else)
        /// </summary>
        /// <returns></returns>
        public byte[] GetAllFrames()
        {
            int dataLength = GetDataLength();
            byte[] ret;

            MemoryStream ms = new MemoryStream();

            try { ms.Write(waveFile, dataOffset, dataLength); ret = ms.ToArray(); }
            catch { ms.Dispose(); throw; }

            ms.Dispose();
            return ret;
        }

        /// <summary>
        /// Trims the start of the wave to the given sample
        /// </summary>
        /// <param name="newStartSample"></param>
        /// <returns></returns>
        public MemoryStream TrimStart(int newStartSample)
        {
            if (newStartSample > this.SampleCount) throw new Exception(string.Format("The loop start sample ({0}) is higher than the total number of samples ({1}) in this file!", newStartSample, this.SampleCount));

            MemoryStream msOut = new MemoryStream();

            try
            {
                msOut.Seek(0, SeekOrigin.Begin);

                if (newStartSample == 0)
                {
                    msOut.Write(waveFile, 0, waveFile.Length);
                    msOut.Seek(0, SeekOrigin.Begin);
                    return msOut;
                }

                msOut.Write(waveFile, 0, this.dataOffset + 8);
                msOut.Write(waveFile, this.ChannelCount * 2 * newStartSample, waveFile.Length - (this.dataOffset + 8 + (this.ChannelCount * 2 * newStartSample)));

                int cutted = (this.ChannelCount * 2 * newStartSample) - (this.dataOffset + 8);

                byte[] newLength = BitConverter.GetBytes((uint)msOut.Position);
                byte[] dataLength = BitConverter.GetBytes(BitConverter.ToInt32(waveFile, this.dataOffset + 4) - cutted);

                msOut.Seek(4, SeekOrigin.Begin);
                msOut.Write(newLength, 0, newLength.Length);
                msOut.Seek(this.dataOffset + 4, SeekOrigin.Begin);
                msOut.Write(dataLength, 0, dataLength.Length);

                msOut.Seek(0, SeekOrigin.Begin);
            }
            catch { msOut.Dispose(); throw; }

            return msOut;
        }

        /// <summary>
        /// Closes the Wave file
        /// </summary>
        public void Close()
        {
            waveFile = null;
        }



        //Private Functions

        private int GetFrameCount()
        {
            int dataLength = GetDataLength();

            return (dataLength / (this.ChannelCount * 2));
        }

        private int GetLoopStart()
        {
            if (smplOffset == -1) return 0;

            byte[] temp = new byte[4];

            temp[0] = waveFile[smplOffset + 52];
            temp[1] = waveFile[smplOffset + 53];
            temp[2] = waveFile[smplOffset + 54];
            temp[3] = waveFile[smplOffset + 55];

            return BitConverter.ToInt32(temp, 0);
        }

        private int GetLoopCount()
        {
            if (smplOffset == -1) return 0;

            byte[] temp = new byte[4];

            temp[0] = waveFile[smplOffset + 36];
            temp[1] = waveFile[smplOffset + 37];
            temp[2] = waveFile[smplOffset + 38];
            temp[3] = waveFile[smplOffset + 39];

            return BitConverter.ToInt32(temp, 0);
        }

        private bool GetSmplOffset()
        {
            try
            {
                int length = (waveFile.Length > 5004 ? (waveFile.Length - 5000) : 0);
                for (int i = waveFile.Length - 4; i > length; i--)
                {
                    if (waveFile[i] == 's' &&
                        waveFile[i + 1] == 'm' &&
                        waveFile[i + 2] == 'p' &&
                        waveFile[i + 3] == 'l')
                    {
                        this.smplOffset = i;
                        return true;
                    }
                }
            }
            catch { }

            for (int i = 0; i < waveFile.Length - 4; i++)
            {
                if (waveFile[i] == 's' &&
                    waveFile[i + 1] == 'm' &&
                    waveFile[i + 2] == 'p' &&
                    waveFile[i + 3] == 'l')
                {
                    this.smplOffset = i;
                    return true;
                }
            }

            this.smplOffset = -1;
            return false;
        }

        private bool GetFmtOffset()
        {
            if (waveFile[12] == 'f' &&
                waveFile[13] == 'm' &&
                waveFile[14] == 't' &&
                waveFile[15] == ' ')
            {
                this.fmtOffset = 12;
                return true;
            }

            int length = (waveFile.Length > 5004 ? 5000 : waveFile.Length - 4);
            for (int i = 0; i < length; i++)
            {
                if (waveFile[i] == 'f' &&
                    waveFile[i + 1] == 'm' &&
                    waveFile[i + 2] == 't' &&
                    waveFile[i + 3] == ' ')
                {
                    this.fmtOffset = i;
                    return true;
                }
            }

            this.fmtOffset = -1;
            return false;
        }

        private int GetDataLength()
        {
            byte[] temp = new byte[4];

            temp[0] = waveFile[dataOffset + 4];
            temp[1] = waveFile[dataOffset + 5];
            temp[2] = waveFile[dataOffset + 6];
            temp[3] = waveFile[dataOffset + 7];

            return BitConverter.ToInt32(temp, 0);
        }

        private bool GetDataOffset()
        {
            if (waveFile[40] == 'd' &&
                waveFile[41] == 'a' &&
                waveFile[42] == 't' &&
                waveFile[43] == 'a')
            {
                this.dataOffset = 40;
                return true;
            }

            for (int i = 0; i < waveFile.Length - 4; i++)
            {
                if (waveFile[i] == 'd' &&
                waveFile[i + 1] == 'a' &&
                waveFile[i + 2] == 't' &&
                waveFile[i + 3] == 'a')
                {
                    this.dataOffset = i;
                    return true;
                }
            }

            this.dataOffset = -1;
            return false;
        }

        private int GetSampleRate()
        {
            byte[] temp = new byte[4];

            temp[0] = waveFile[fmtOffset + 12];
            temp[1] = waveFile[fmtOffset + 13];
            temp[2] = waveFile[fmtOffset + 14];
            temp[3] = waveFile[fmtOffset + 15];

            return BitConverter.ToInt32(temp, 0);
        }

        private int GetBitDepth()
        {
            byte[] temp = new byte[2];

            temp[0] = waveFile[fmtOffset + 22];
            temp[1] = waveFile[fmtOffset + 23];

            return BitConverter.ToInt16(temp, 0);
        }

        private int GetChannelCount()
        {
            byte[] temp = new byte[2];

            temp[0] = waveFile[fmtOffset + 10];
            temp[1] = waveFile[fmtOffset + 11];

            return BitConverter.ToInt16(temp, 0);
        }

        private int GetDataFormat()
        {
            byte[] temp = new byte[2];

            temp[0] = waveFile[fmtOffset + 8];
            temp[1] = waveFile[fmtOffset + 9];

            return BitConverter.ToInt16(temp, 0);
        }

        private bool CheckWave()
        {
            if (waveFile[0] != 'R' ||
                waveFile[1] != 'I' ||
                waveFile[2] != 'F' ||
                waveFile[3] != 'F' ||

                waveFile[8] != 'W' ||
                waveFile[9] != 'A' ||
                waveFile[10] != 'V' ||
                waveFile[11] != 'E') return false;

            return true;
        }
    }
}
