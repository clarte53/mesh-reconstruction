using System;
using UnityEngine;

public class OpenVRTracking : MonoBehaviour
{
	#region Members
	public event GetHeadPose getHeadPose;

	[Tooltip("Tracking update period, in ms")]
	public uint refreshPeriod = 10;

	protected System.Diagnostics.Process process;
	#endregion

	#region Delegates
	public delegate void GetHeadPose(ulong timestamp, Matrix4x4 m);
	#endregion

	#region MonoBehaviour callbacks
	protected void Start()
	{
		process = new System.Diagnostics.Process();

		process.StartInfo.FileName = Application.dataPath + "/../openvr-tracking.exe";
		process.StartInfo.Arguments = refreshPeriod.ToString();
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardInput = true;
		process.StartInfo.RedirectStandardOutput = true;
		process.OutputDataReceived += ProcessDataReceived;
		process.Start();
		process.BeginOutputReadLine();
	}

	protected void OnDestroy()
	{
		if(process != null)
		{
			process.StandardInput.WriteLine(); // Send signal to close the application

			System.Threading.Thread.Sleep(Mathf.Max(3 * (int) refreshPeriod, 100));

			if(!process.HasExited)
			{
				Debug.LogWarning("Tracking process not responding. Killing the process.");

				process.Kill();
			}

			process.Dispose();
		}
	}
	#endregion

	#region Internal methods
	protected void ProcessDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs received)
	{
		const int nb_floats = 12;
		const int float_size = 2 * sizeof(float);
		const int long_size = 2 * sizeof(long);

		float[] values = new float[nb_floats];
		byte[] bytes;

		if(received.Data != null)
		{
			ulong timestamp = ulong.Parse(received.Data.Substring(0, long_size), System.Globalization.NumberStyles.HexNumber);

			if(BitConverter.IsLittleEndian)
			{
				bytes = BitConverter.GetBytes(timestamp);

				Array.Reverse(bytes);

				timestamp = BitConverter.ToUInt64(bytes, 0);
			}

			for(int i = 0; i < nb_floats; i++)
			{
				bytes = BitConverter.GetBytes(uint.Parse(received.Data.Substring(i * float_size + long_size, float_size), System.Globalization.NumberStyles.HexNumber));

				if(BitConverter.IsLittleEndian)
				{
					Array.Reverse(bytes);
				}

				values[i] = BitConverter.ToSingle(bytes, 0);
			}

			Matrix4x4 m = Matrix4x4.identity;

			m[0, 0] = values[0];
			m[0, 1] = values[1];
			m[0, 2] = -values[2];
			m[0, 3] = values[3];

			m[1, 0] = values[4];
			m[1, 1] = values[5];
			m[1, 2] = -values[6];
			m[1, 3] = values[7];

			m[2, 0] = -values[8];
			m[2, 1] = -values[9];
			m[2, 2] = values[10];
			m[2, 3] = -values[11];

			try
			{
				getHeadPose?.Invoke(timestamp, m);
			}
			catch(Exception exception)
			{
				Debug.LogErrorFormat("Error in delegate: {0} - {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
			}
		}
	}
	#endregion
}
