using System;
using UnityEngine;

namespace SmartStage
{
	public class TextureUtils
	{

		private static void swap(ref int a, ref int b)
		{
			int tmp = a;
			a = b;
			b = tmp;
		}

		public static void drawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color colour)
		{
			bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
			if (steep)
			{
				swap(ref x0, ref y0);
				swap(ref x1, ref y1);
			}
			if (x0 > x1)
			{
				swap(ref x0, ref x1);
				swap(ref y0, ref y1);
			}

			int dX = (x1 - x0);
			int dY = Math.Abs(y1 - y0);
			int err = (dX / 2);
			int ystep = (y0 < y1 ? 1 : -1);
			int y = y0;

			for (int x = x0; x <= x1; ++x)
			{
				if (steep)
				{
					if (y >=0 && y < texture.width && x >= 0 && x < texture.height)
						texture.SetPixel(y, x, colour);
				}
				else
				{
					if (x >=0 && x < texture.width && y >= 0 && y < texture.height)
						texture.SetPixel(x, y, colour);
				}
				err = err - dY;
				if (err < 0)
				{
					y += ystep;
					err += dX;
				}
			}
		}
	}
}

