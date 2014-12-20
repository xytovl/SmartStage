using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{

	public class Scale
	{
		public double min;
		public double max;
		private int width;
		public Scale(double min, double max, int width)
		{
			this.min = min;
			this.max = max;
			this.width = width-1;
		}
		public int toPlot(double val)
		{
			return (int)(width * (val - min) / (max - min));
		}
		public double fromPlot(int scaled)
		{
			return min + (double)scaled/width * (max - min) ;
		}
	}

	class AscentPlot
	{
		public Texture2D texture;

		private List<Sample> samples;
		private List<Sample> zoomedSamples;
		private List<StageDescription> stages;
		private List<PlotElement> plots = new List<PlotElement>();

		private Scale timeScale;

		private int selectedTime;
		private int? hoveredPoint;
		private bool mouseDown;

		static GUIStyle selectionStyle;

		static AscentPlot()
		{
			selectionStyle = new GUIStyle();
			Texture2D background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			background.SetPixel(0,0, new Color(0, 0, 1, 0.3f));
			background.Apply();
			selectionStyle.normal.background = background;
		}

		public AscentPlot (List<Sample> samples, List<StageDescription> stages, int xdim, int ydim)
		{
			this.samples = samples;
			zoomedSamples = samples;
			this.stages = stages;
			timeScale = new Scale(samples.First().time, samples.Last().time, xdim);
			plots.Add(new PlotElement("acceleration", "m/s²", s => s.acceleration, new Color(0.3f, 0.3f, 1), fixedMin:0));
			plots.Add(new PlotElement("surface velocity", "m/s", s => s.velocity, new Color(1, 0.3f, 0.3f)));
			plots.Add(new PlotElement("altitude", "m", s => s.altitude, new Color(0.5f, 0.5f, 0.3f), false));
			plots.Add(new PlotElement("throttle", "%", s => s.throttle * 100, new Color(0.3f, 0.3f, 0.3f), fixedMin:0, fixedMax:100));
			texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false);
			drawTexture();
		}

		private int[] validRange(Scale timeScale)
		{
			int[] res = {0,samples.Count()};
			for (int i = 1 ; i < samples.Count() ; i++)
			{
				if (samples[i].time < timeScale.min)
					res[0] = i;
				if (samples[i].time > timeScale.max)
				{
					res[1] = i;
					return res;
				}
			}
			return res;
		}

		private void drawTexture()
		{
			fillBackground();
			if (hoveredPoint != null)
				TextureUtils.drawLine(texture, hoveredPoint.Value, 0, hoveredPoint.Value, texture.height, new Color(0.4f, 0.4f, 0));
			foreach(var e in plots)
				e.draw(texture, timeScale, zoomedSamples);
			texture.Apply();
		}

		private void fillBackground()
		{
			Color even = new Color(0,0,0);
			Color odd = new Color(0.2f, 0.2f, 0.2f);
			int x = 0;
			Color colour = even;
			foreach (var stage in stages)
			{
				for (;x < texture.width && timeScale.fromPlot(x) < stage.activationTime; x++)
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

		public void rescale(double t0, double t1)
		{
			timeScale.min = Math.Max(samples.First().time, Math.Min(t0, t1));
			timeScale.max = Math.Min(samples.Last().time, Math.Max(t0, t1));
			int minIndex = 0;
			int maxIndex = samples.Count;
			for (int i = 0 ; i < maxIndex ; ++i)
			{
				if (samples[i].time < timeScale.min)
					minIndex = i;
				if (samples[i].time > timeScale.max)
					maxIndex = i;
			}
			zoomedSamples = samples.GetRange(minIndex, maxIndex - minIndex);
		}

		public bool draw()
		{
			GUILayout.BeginHorizontal();
			GUILayout.Box(texture, GUIStyle.none, new GUILayoutOption[] { GUILayout.Width(texture.width), GUILayout.Height(texture.height)});
			if (Event.current.type == EventType.Repaint)
			{
				var rect = GUILayoutUtility.GetLastRect(); rect.x +=1; rect.y += 2;
				Vector2 mouse = Event.current.mousePosition;
				if (rect.Contains(mouse))
				{
					hoveredPoint = (int)(mouse - rect.position).x;
					GUI.changed = true;
				}
				else
				{
					GUI.changed |= hoveredPoint != null;
					hoveredPoint = null;
					mouseDown = false;
				}
				if (mouseDown)
				{
					GUI.Box(new Rect(rect.x + Math.Min(selectedTime, hoveredPoint.Value),
						rect.y,
						Math.Abs(selectedTime - hoveredPoint.Value),
						rect.height), "", selectionStyle);
				}
			}
			if (hoveredPoint != null)
			{
				switch (Event.current.type)
				{
				case EventType.MouseDown:
					if (Event.current.button == 0)
					{
						mouseDown = true;
						selectedTime = hoveredPoint.Value;
					}
					break;
				case EventType.MouseUp:
					if (Event.current.button == 0 && mouseDown)
					{
						mouseDown = false;
						if (Math.Abs(selectedTime - hoveredPoint.Value) > 5)
						{
							rescale(selectedTime, hoveredPoint.Value);
						}
					}
					break;
				case EventType.ScrollWheel:
					if (Event.current.delta.y == 0)
						break;
					double lambda = Event.current.delta.y < 0 ? 0.9 : 1/0.9;
					double deltax = timeScale.max - timeScale.min;

					double newminx = timeScale.fromPlot(hoveredPoint.Value) - hoveredPoint.Value * lambda * deltax / texture.width;
					newminx = Math.Max(newminx, 0);
					double newmaxx = Math.Min(newminx + lambda * deltax, samples.Last().time);
					rescale(newminx, newmaxx);

					break;
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
				{
					double time = timeScale.fromPlot(hoveredPoint.Value);
					GUILayout.Label(Math.Round(e.value(time, zoomedSamples), 2).ToString() + e.unit);
				}
				else
					GUILayout.Label("");
			}
			if (hoveredPoint != null)
			{
				GUILayout.Label("time");
				GUILayout.Label(Math.Round(timeScale.fromPlot(hoveredPoint.Value)).ToString() + "s");
			}

			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			if (GUI.changed)
				drawTexture();

			return hoveredPoint == null;
		}
	}
}

