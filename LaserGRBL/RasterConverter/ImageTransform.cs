﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;


namespace LaserGRBL.RasterConverter
{
	public class ImageTransform
	{
		public static Bitmap ResizeImage(Image image, Size size, bool killalfa, InterpolationMode interpolation)
		{
			if (image.Size == size)
				return new Bitmap((Image)image.Clone());

			Rectangle destRect = new Rectangle(0, 0, size.Width, size.Height);
			Bitmap destImage = new Bitmap(size.Width, size.Height);

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (Graphics g = Graphics.FromImage(destImage))
			{
				if (killalfa)
					g.Clear(Color.White);
				
				
				if (killalfa)
					g.CompositingMode = CompositingMode.SourceOver;
				else
					g.CompositingMode = CompositingMode.SourceCopy;
				
				g.CompositingQuality = CompositingQuality.HighQuality;
				g.SmoothingMode = SmoothingMode.HighQuality;
				g.InterpolationMode = interpolation;

				
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

				using (System.Drawing.Imaging.ImageAttributes wrapMode = new System.Drawing.Imaging.ImageAttributes())
				{
					wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
					g.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

		public static Bitmap Threshold(Image img, float threshold, bool apply)
		{
			Bitmap bmp = new Bitmap(img.Width, img.Height);

			using (Graphics g = Graphics.FromImage(bmp))
			{
				g.Clear(Color.White); //Threshold is the final transformation 
				g.DrawImage(img, 0,0); //so clear any transparent color and apply threshold
				
				// Create an ImageAttributes object, and set its color threshold.
				ImageAttributes imageAttr = new ImageAttributes();
				imageAttr.SetThreshold(threshold);

				if (apply)
					g.DrawImage(bmp, new Rectangle(0,0, bmp.Width, bmp.Height), 0,0, bmp.Width, bmp.Height,	GraphicsUnit.Pixel, imageAttr);
			}
			return bmp;
		}

		private static Bitmap draw_adjusted_image(Image img, ColorMatrix cm)
		{

			try {
				Bitmap tmp = new Bitmap(img.Width, img.Height);
				// create a copy of the source image 
				using (Graphics g = Graphics.FromImage(tmp)) {
					g.Clear(Color.Transparent);

					ImageAttributes imgattr = new ImageAttributes();
					Rectangle rc = new Rectangle(0, 0, img.Width, img.Height);
					// associate the ColorMatrix object with an ImageAttributes object
					imgattr.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

					// draw the copy of the source image back over the original image, 
					//applying the ColorMatrix

					g.DrawImage(img, rc, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, imgattr);
				}
				return tmp;
			} catch {
				return null;
			}

		}




		//**************************

		public enum Formula
		{
			SimpleAverage = 0,
			WeightAverage = 1,
			OpticalCorrect = 2,
			Custom = 3
		}

		public static Bitmap DitherImage(Bitmap img)
		{
			Bitmap image;
			Cyotek.Drawing.ArgbColor[] originalData;
			Size size;
			Cyotek.Drawing.Imaging.ColorReduction.IErrorDiffusion dither;

			image = img;
			size = image.Size;

			originalData = Cyotek.DitheringTest.Helpers.ImageUtilities.GetPixelsFrom32BitArgbImage(image);

			dither = new Cyotek.Drawing.Imaging.ColorReduction.FloydSteinbergDithering();

			for (int row = 0; row < size.Height; row++)
			{
				for (int col = 0; col < size.Width; col++)
				{
					int index;
					Cyotek.Drawing.ArgbColor current;
					Cyotek.Drawing.ArgbColor transformed;

					index = row * size.Width + col;

					current = originalData[index];

					// transform the pixel - normally this would be some form of color
					// reduction. For this sample it's simple threshold based
					// monochrome conversion
					transformed = TransformPixel(current);
					originalData[index] = transformed;

					// apply a dither algorithm to this pixel
					if (dither != null)
					{
						dither.Diffuse(originalData, current, transformed, col, row, size.Width, size.Height);
					}
				}
			}

			return Cyotek.DitheringTest.Helpers.ImageUtilities.ToBitmap(originalData, size);
		}

		private static Cyotek.Drawing.ArgbColor TransformPixel(Cyotek.Drawing.ArgbColor pixel)
		{
			byte gray;

			gray = (byte)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);

			/*
			 * I'm leaving the alpha channel untouched instead of making it fully opaque
			 * otherwise the transparent areas become fully black, and I was getting annoyed
			 * by this when testing images with large swathes of transparency!
			 */

			return gray < 128 ? new Cyotek.Drawing.ArgbColor(pixel.A, 0, 0, 0) : new Cyotek.Drawing.ArgbColor(pixel.A, 255, 255, 255);
		}


		public static Bitmap GrayScale(Image img, float R, float G, float B, float brightness, float contrast, Formula formula)
		{
			ColorMatrix cm = default(ColorMatrix);

			// Apply selected grayscale formula
			
			float RedFactor = 0;
			float GreenFactor = 0;
			float BlueFactor = 0;
			
			if (formula == Formula.SimpleAverage)
			{
				RedFactor = 0.333F;
				GreenFactor = 0.333F;
				BlueFactor = 0.333F;
			}
			if (formula == Formula.WeightAverage)
			{
				RedFactor = 0.333F;
				GreenFactor = 0.444F;
				BlueFactor = 0.222F;
			}
			else if (formula == Formula.OpticalCorrect) // Reference: http://www.had2know.com/technology/rgb-to-gray-scale-converter.html
            {
				RedFactor = 0.299F;
				GreenFactor = 0.587F;
				BlueFactor = 0.114F;				
            }
			else if (formula == Formula.Custom)
			{
				RedFactor =	 0.333F * R;
				GreenFactor = 0.333F * G;
				BlueFactor = 0.333F * B;
			}
			
			RedFactor = RedFactor * contrast;
			GreenFactor = GreenFactor * contrast;
			BlueFactor = BlueFactor * contrast;
			
			cm = new ColorMatrix(new float[][] {
				new float[] {RedFactor,RedFactor,RedFactor,0F,0F},
				new float[] {GreenFactor,GreenFactor,GreenFactor,0F,0F},
				new float[] {BlueFactor,BlueFactor,BlueFactor,0F,0F},
				new float[] {0F,0F,0F,1F,0F},
				new float[] {brightness,brightness,brightness,0F,1F}
			});
		
            
			return draw_adjusted_image(img, cm);

		}
	}

}