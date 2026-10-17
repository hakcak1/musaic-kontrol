// Uygulamadaki Theme.DrawLogo çizimini kullanarak çok boyutlu musaic.ico (ve önizleme PNG'si) üretir.
// Kullanım: ikon_olustur.exe <MusaicKontrol.exe> <çıkış.ico> [önizleme.png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

class IkonOlustur
{
    static void Main(string[] args)
    {
        var asm = Assembly.LoadFrom(args[0]);
        var logo = asm.GetType("MusaicKontrol.Theme").GetMethod("LogoBitmap");
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int s in sizes)
            using (var bmp = (Bitmap)logo.Invoke(null, new object[] { s }))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                images.Add(ms.ToArray());
            }

        using (var fs = new FileStream(args[1], FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }

        if (args.Length > 2)
        {
            // Önizleme: koyu zemin üzerinde büyük logo + küçük boyutlar
            using (var sheet = new Bitmap(560, 300))
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(18, 18, 21));
                using (var big = (Bitmap)logo.Invoke(null, new object[] { 256 })) g.DrawImage(big, 20, 22);
                int x = 300;
                foreach (int s in new[] { 128, 48, 32, 16 })
                {
                    using (var b = (Bitmap)logo.Invoke(null, new object[] { s })) g.DrawImage(b, x, 22 + (128 - s) / 2);
                    x += s + 14;
                }
                using (var light = new SolidBrush(Color.FromArgb(240, 240, 240))) g.FillRectangle(light, 300, 170, 240, 110);
                x = 312;
                foreach (int s in new[] { 64, 32, 24, 16 })
                {
                    using (var b = (Bitmap)logo.Invoke(null, new object[] { s })) g.DrawImage(b, x, 193 + (64 - s) / 2);
                    x += s + 16;
                }
                sheet.Save(args[2], ImageFormat.Png);
            }
        }
        Console.WriteLine("Simge üretildi: " + args[1]);
    }
}
