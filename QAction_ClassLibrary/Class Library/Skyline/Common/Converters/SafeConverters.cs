namespace Skyline.DataMiner.Library.Common.SafeConverters
{
	using System;

	/// <summary>
	/// Contains methods allowing safe conversion between data types.
	/// </summary>
	public static class SafeConvert
	{
		/// <summary>
		/// Performs a safe conversion from <see cref="System.Double"/> to <see cref="System.UInt32"/>.
		/// </summary>
		/// <param name="value">The double-precision floating-point number to convert.</param>
		/// <returns>The converted value, rounded to the nearest <see cref="System.UInt32"/>.</returns>
		/// <remarks>
		/// <para>When polling a <see cref="System.UInt32"/> value from a data source, DM will convert it to a <see cref="System.Double"/> in order to store the value into a parameter.</para>
		/// <para>In some cases, such conversion might result into a rounded value bigger than the <see cref="System.UInt32.MaxValue"/>
		/// which would in turn cause an overflow when trying to convert that <see cref="System.Double"/> back to a <see cref="System.UInt32"/> in a QAction.<br/>
		/// This method covers such cases by returning <see cref="System.UInt32.MaxValue"/> in case of such overflow.</para>
		/// <para>If value is halfway between two whole numbers, the even number is returned.</para>
		/// <para>If value is higher than the max value of a <see cref="System.UInt32"/>, the max value is returned.</para>
		/// </remarks>
		public static uint ToUInt32(double value)
		{
			try
			{
				return Convert.ToUInt32(value);
			}
			catch (OverflowException)
			{
				return UInt32.MaxValue;
			}
		}

		/// <summary>
		/// Performs a safe conversion from <see cref="System.Double"/> to <see cref="System.UInt64"/>.
		/// </summary>
		/// <param name="value">The double-precision floating-point number to convert.</param>
		/// <returns>value, rounded to the nearest <see cref="System.UInt64"/>.</returns>
		/// <remarks>
		/// <para>When polling a <see cref="System.UInt64"/> value from a data source, DM will convert it to a <see cref="System.Double"/> in order to store the value into a parameter.</para>
		/// <para>In some cases, such conversion might result into a rounded value bigger than the <see cref="System.UInt64.MaxValue"/>
		/// which would in turn cause an overflow when trying to convert that <see cref="System.Double"/> back to a <see cref="System.UInt64"/> in a QAction.<br/>
		/// This method covers such cases by returning <see cref="System.UInt64.MaxValue"/> in case of such overflow.</para>
		/// <para>If value is halfway between two whole numbers, the even number is returned.</para>
		/// <para>If value is higher than the max value of a <see cref="System.UInt64"/>, the max value is returned.</para>
		/// </remarks>
		public static ulong ToUInt64(double value)
		{
			try
			{
				return Convert.ToUInt64(value);
			}
			catch (OverflowException)
			{
				return UInt64.MaxValue;
			}
		}
	}
}
