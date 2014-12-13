using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{
	public class AscentPlot
	{
		public Texture2D texture;

		private List<Sample> samples;
		private List<StageDescription> stages;
		private List<PlotElement> plots = new List<PlotElement>();
		public int[] hoveredPoint;

		private class PlotElement
		{
			Color colour;
			public readonly string name;
			public readonly string unit;
			List<double> time;
			List<double> values;
			public bool active;
			public float pulse;
			public readonly GUIStyle buttonStyle;

			public PlotElement(string name, string unit, List<double> time, List<double> values, Color colour, bool active = true)
			{
				this.name = name;
				this.unit = unit;
				this.time = time;
				this.values = values;
				this.colour = colour;
				this.active = active;
				var textColour = Color.Lerp(colour, Color.white, 0.3f);
				active = true;
				buttonStyle = new GUISkin().button;
				buttonStyle.normal.textColor = textColour;
				buttonStyle.hover.textColor = textColour;
				buttonStyle.active.textColor = textColour;
			}

			public double value(double timePercent)
			{
				double timeVal = timePercent * time.Last();
				for (int i = 1 ; i < time.Count() ; i++)
				{
					if (time[i] > timeVal)
					{
						double r = (time[i] - timeVal ) / (time[i] - time[i-1]);
						return values[i-1] + r * (values[i] - values[i-1]);
					}
				}
				return values.Last();
			}

			public void draw(Texture2D texture)
			{
				if (!active)
					return;

				Color pulsed = Color.Lerp(Color.white, colour, (float)Math.Pow(Math.Cos(pulse)/2 + 1, 3));

				double timeToX = (texture.width - 1) / time.Last();
				double valToY = (texture.height - 1) / values.Max();
				for (int i = 1 ; i < time.Count() ; i++)
				{
					drawLine(texture,
						(int)(time[i-1] * timeToX), (int)(values[i-1] * valToY),
						(int)(time[i] * timeToX), (int)(values[i] * valToY),
						pulsed);
				}
			}
		}

		public AscentPlot (List<Sample> samples, List<StageDescription> stages, int xdim, int ydim)
		{
			this.samples = samples;
			this.stages = stages;
			List<double> times = samples.ConvertAll(s => s.time);
			plots.Add(new PlotElement("acceleration", "m/s²", times, samples.ConvertAll(s => s.acceleration), new Color(0.3f, 0.3f, 1)));
			plots.Add(new PlotElement("surface velocity", "m/s", times, samples.ConvertAll(s => s.velocity), new Color(1, 0.3f, 0.3f)));
			plots.Add(new PlotElement("altitude", "m", times, samples.ConvertAll(s => s.altitude), new Color(0.5f, 0.5f, 0.3f), false));
			plots.Add(new PlotElement("throttle", "%", times, samples.ConvertAll(s => s.throttle * 100), new Color(0.3f, 0.3f, 0.3f)));
			texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false);
			drawTexture();
		}

		private static void swap(ref int a, ref int b)
		{
			int tmp = a;
			a = b;
			b = tmp;
		}

		private static void drawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color colour)
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
					texture.SetPixel(y, x, colour);
				else
					texture.SetPixel(x, y, colour);
				err = err - dY;
				if (err < 0)
				{
					y += ystep;
					err += dX;
				}
			}
		}

		private void drawTexture()
		{
			fillBackground();
			if (hoveredPoint != null)
				drawLine(texture, hoveredPoint[0], 0, hoveredPoint[0], texture.height, new Color(0.4f, 0.4f, 0));
			foreach(var e in plots)
				e.draw(texture);
			texture.Apply();
		}

		private void fillBackground()
		{
			Color even = new Color(0,0,0);
			Color odd = new Color(0.2f, 0.2f, 0.2f);
			double xToTime = samples.Last().time / texture.width;
			int x = 0;
			Color colour = even;
			foreach (var stage in stages)
			{
				for (;x * xToTime < stage.activationTime; x++)
				{
					for (int y = 0 ; y < texture.height ; y++)
					{
						texture.SetPixel(x, y, colour);
					}
				}
				colour = (colour == even) ? odd : even;
			}
			for (;x < texture.width ; x++)
			{
				for (int y = 0 ; y < texture.height ; y++)
				{
					texture.SetPixel(x, y, colour);
				}
			}
		}

		public void draw()
		{
			GUILayout.BeginHorizontal();
			GUILayout.Box(texture, GUIStyle.none, new GUILayoutOption[] { GUILayout.Width(texture.width), GUILayout.Height(texture.height)});
			if (Event.current.type == EventType.Repaint)
			{
				var rect = GUILayoutUtility.GetLastRect(); rect.x +=1; rect.y += 2;
				Vector2 mouse = Event.current.mousePosition;
				if (rect.Contains(mouse))
				{
					var pos = (mouse - rect.position);
					hoveredPoint = new int[]{(int) pos.x, (int)(rect.height - pos.y - 1)};
					GUI.changed = true;
				}
				else
				{
					GUI.changed |= hoveredPoint != null;
					hoveredPoint = null;
				}
			}
			GUILayout.BeginVertical();
			foreach (var e in plots)
			{
				if (GUILayout.Button(e.name, e.buttonStyle))
					e.active = ! e.active;

				if (Event.current.type == EventType.Repaint)
				{
					if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
					{
						e.pulse = (float)(Time.time * 2 * Math.PI);
						GUI.changed = true;
					}
					else
					{
						if (e.pulse != 0)
							GUI.changed = true;
						e.pulse = 0;
					}
				}

				if (hoveredPoint != null)
					GUILayout.Label(Math.Round(e.value((double)hoveredPoint[0] / texture.width), 2).ToString() + e.unit);
				else
					GUILayout.Label("");
			}
			if (hoveredPoint != null)
			{
				GUILayout.Label("time");
				GUILayout.Label(Math.Round(hoveredPoint[0] * samples.Last().time / texture.width).ToString() + "s");
			}

			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			if (GUI.changed)
				drawTexture();
		}
	}
}

