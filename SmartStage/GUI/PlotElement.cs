using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{
	class PlotElement
	{
		Color colour;
		public readonly string name;
		public readonly string unit;
		public bool active;
		public float pulse;
		public readonly GUIStyle buttonStyle;
		private readonly Func<Sample, double> selector;

		public PlotElement(string name, string unit, Func<Sample, double> selector, Color colour, bool active = true)
		{
			this.name = name;
			this.unit = unit;
			this.selector = selector;
			this.colour = colour;
			this.active = active;
			var textColour = Color.Lerp(colour, Color.white, 0.3f);
			active = true;
			buttonStyle = new GUISkin().button;
			buttonStyle.normal.textColor = textColour;
			buttonStyle.hover.textColor = textColour;
			buttonStyle.active.textColor = textColour;
		}

		public double value(double timeVal, List<Sample> samples)
		{
			for (int i = 1 ; i < samples.Count() ; i++)
			{
				if (samples[i].time > timeVal)
				{
					double r = (samples[i].time - timeVal ) / (samples[i].time - samples[i-1].time);
					return selector(samples[i-1]) + r * (selector(samples[i]) - selector(samples[i-1]));
				}
			}
			return selector(samples.Last());
		}

		public void draw(Texture2D texture, Scale timeScale, List<Sample> samples)
		{
			if (!active)
				return;

			Color pulsed = Color.Lerp(Color.white, colour, (float)Math.Pow(Math.Cos(pulse)/2 + 1, 3));
			Scale valScale = new Scale(samples.Min(selector), samples.Max(selector), texture.width);
			for (int i = 1 ; i < samples.Count() ; i++)
			{
				TextureUtils.drawLine(texture,
					timeScale.toPlot(samples[i-1].time), valScale.toPlot(selector(samples[i-1])),
					timeScale.toPlot(samples[i].time), valScale.toPlot(selector(samples[i])),
					pulsed);
			}
		}
	}
}

