using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PhotoshopFile;

namespace PsdTestBed
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Creating PSD File...");
            PsdFile psd = new PsdFile();
            psd.BitDepth = 8;
            psd.ColorMode = PsdColorMode.RGB;
            psd.ChannelCount = 3;
            psd.Resolution = new ResolutionInfo()
            {
                WidthDisplayUnit = ResolutionInfo.Unit.Inches,
                HeightDisplayUnit = ResolutionInfo.Unit.Inches,

                HResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                VResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,

                HDpi = new UFixed16_16(96.0),
                VDpi = new UFixed16_16(96.0),
            };
            psd.ImageCompression = ImageCompression.Rle;
            psd.ColumnCount = 128;
            psd.RowCount = 128;

            Console.WriteLine("Creating bitmaps...");
            Bitmap layer1, layer2, baseLayer;
            Console.WriteLine("Creating Layer 1 bitmap...");
            layer1 = new Bitmap(128, 128);
            Graphics g = Graphics.FromImage(layer1);
            Console.WriteLine("Filling Layer 1 bitmap with color #726762");
            g.FillRectangle(new SolidBrush(Color.FromArgb('r', 'g', 'b')), 0, 0, 128, 128);
            g.Dispose();
            Console.WriteLine("Creating Layer 1 bitmap...");
            layer2 = new Bitmap(128, 128);
            g = Graphics.FromImage(layer2);
            Console.WriteLine("Filling Layer 1 bitmap with color #524742");
            g.FillRectangle(new SolidBrush(Color.FromArgb('R', 'G', 'B')), 0, 0, 128, 128);
            g.Dispose();
            baseLayer = layer1;

            Console.WriteLine("Creating Layer 1 as \"Frame 1\"...");
            Layer layer1psd, layer2psd;
            layer1psd = new Layer(psd);
            layer1psd.Name = "Frame 1";
            layer1psd.Opacity = 255;
            layer1psd.Visible = true;
            layer1psd.Rect = new Rectangle(0, 0, layer1.Width, layer1.Height);
            layer1psd.Clipping = false;
            layer1psd.CreateChannelsFromImage(layer1);
            layer1psd.Masks = new MaskInfo();
            layer1psd.BlendingRangesData = new BlendingRanges(layer1psd);
            psd.Layers.Add(layer1psd);

            Console.WriteLine("Creating Layer 2 as \"Frame 2\"...");
            layer2psd = new Layer(psd);
            layer2psd.Name = "Frame 2";
            layer2psd.Opacity = 255;
            layer2psd.Visible = true;
            layer2psd.Rect = new Rectangle(0, 0, layer2.Width, layer2.Height);
            layer2psd.Clipping = false;
            layer2psd.CreateChannelsFromImage(layer2);
            layer2psd.Masks = new MaskInfo();
            layer2psd.BlendingRangesData = new BlendingRanges(layer2psd);
            psd.Layers.Add(layer2psd);

            Console.WriteLine("Creating base layer...");
            psd.BaseLayer = new Layer(psd);
            psd.BaseLayer.Rect = new Rectangle(0, 0, 128, 128);
            psd.BaseLayer.CreateChannelsFromImage(baseLayer);

            Console.WriteLine("Exporting PSD as \"test.psd\"...");
            psd.Save("test.psd", Encoding.Default);

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
