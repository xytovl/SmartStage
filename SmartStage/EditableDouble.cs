using System;
using System.Text.RegularExpressions;

namespace SmartStage
{
	public class EditableDouble
	{
		public double _val;
		public virtual double val
		{
			get { return _val; }
			set
			{
				_val = value;
				_text = (_val / multiplier).ToString();
			}
		}
		public readonly double multiplier;

		public bool parsed;
		public string _text;
		public virtual string text
		{
			get { return _text; }
			set
			{
				_text = value;
				_text = Regex.Replace(_text, @"[^\d+-.]", ""); //throw away junk characters
				double parsedValue;
				parsed = double.TryParse(_text, out parsedValue);
				if (parsed) _val = parsedValue * multiplier;
			}
		}

		public EditableDouble() : this(0) { }

		public EditableDouble(double val, double multiplier = 1)
		{
			this.val = val;
			this.multiplier = multiplier;
			_text = (val / multiplier).ToString();
		}

		public static implicit operator double(EditableDouble x)
		{
			return x.val;
		}
	}
}

