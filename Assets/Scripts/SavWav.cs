// This is a free utility script to save an AudioClip to a .wav file
// Credit to the Unity community
using System;
using System.IO;
using UnityEngine;

public static class SavWav
{
    const int HEADER_SIZE = 44;

    public static void Save(string filepath, AudioClip clip)
    {
        if (!filepath.EndsWith(".wav"))
        {
            filepath += ".wav";
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filepath));

        using (var fileStream = new FileStream(filepath, FileMode.Create))
        using (var writer = new BinaryWriter(fileStream))
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);

            var intData = new short[data.Length];
            var bytes = new byte[data.Length * 2];

            float rescaleFactor = 32767;

            for (int i = 0; i < data.Length; i++)
            {
                intData[i] = (short)(data[i] * rescaleFactor);
                var b = BitConverter.GetBytes(intData[i]);
                bytes[i * 2] = b[0];
                bytes[i * 2 + 1] = b[1];
            }

            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(HEADER_SIZE + bytes.Length);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}