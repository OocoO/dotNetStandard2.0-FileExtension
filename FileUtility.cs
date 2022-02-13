using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Carotaa.Utility
{
	public static class FileUtility
	{
		// Used to solve some Stupid Condition: Writing and reading the same file.
        private static readonly Dictionary<string, object> WriteBuffer = new Dictionary<string, object>();

        public static void ReadAllTextAsync (string filePath, Action<string> onComplete)
        {
	        ReadAllTextAsync(filePath, Encoding.UTF8, onComplete);
        }
        
        public static async void ReadAllTextAsync (string filePath, Encoding encoding, Action<string> onComplete)
		{
			lock (WriteBuffer)
			{
				if (WriteBuffer.ContainsKey(filePath))
				{
					var buffer = (string) WriteBuffer[filePath];
					Loom.QueueOnMainThread(() => onComplete?.Invoke(buffer));
					return;
				}
			}
			
			FileStream sourceStream = null;
			string value = null;
			try
			{
				sourceStream = new FileStream(filePath,
					FileMode.Open, FileAccess.Read, FileShare.Read,
					bufferSize: 4096, useAsync: true);
				StringBuilder sb = new StringBuilder();

				byte[] buffer = new byte[0x1000];
				int numRead;
				while ((numRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
				{
					string text = encoding.GetString(buffer, 0, numRead);
					sb.Append(text);
				}

				value = sb.ToString();
			}
			catch (Exception e)
			{
				LogError(e, filePath);
			}
			finally
			{
				sourceStream?.Close();
			}
			
			Loom.QueueOnMainThread(() => onComplete?.Invoke(value));
		}

		
		public static async void ReadAllBytesAsync (string filePath, Action<byte[]> onComplete)
		{
			lock (WriteBuffer)
			{
				if (WriteBuffer.ContainsKey(filePath))
				{
					var buffer = (byte[]) WriteBuffer[filePath];
					Loom.QueueOnMainThread(() => onComplete?.Invoke(buffer));
					return;
				}
			}
			
			FileStream sourceStream = null;
			byte[] value = null;
			try
			{
				sourceStream = new FileStream(filePath,
					FileMode.Open, FileAccess.Read, FileShare.Read,
					bufferSize: 4096, useAsync: true);
				StringBuilder sb = new StringBuilder();

				value = new byte[sourceStream.Length];
				await sourceStream.ReadAsync(value, 0, (int) sourceStream.Length);
			}
			catch (Exception e)
			{
				LogError(e, filePath);
			}
			finally
			{
				sourceStream?.Close();
			}
			
			Loom.QueueOnMainThread(() => onComplete?.Invoke(value));
		}
		
		public static async void WriteAllBytesAsync (string filePath, byte[] data, Action<WriteResult> onComplete = null)
		{
			lock (WriteBuffer)
			{
				if (WriteBuffer.ContainsKey(filePath))
				{
					throw new Exception($"Writing same file again: {filePath}");
				}
				else
				{
					WriteBuffer.Add(filePath, data);
				}
			}
			
			FileStream sourceStream = null;

			var result = new WriteResult();
			try
			{
				sourceStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
				await sourceStream.WriteAsync(data, 0, data.Length, CancellationToken.None);
				result.Success = true;
			}
			catch (Exception e)
			{
				result.Error = e;
				LogError(e, filePath);
			}
			finally
			{
				sourceStream?.Close();
			}
			
			lock (WriteBuffer)
			{
				WriteBuffer.Remove(filePath);
			}
				
			Loom.QueueOnMainThread(() =>
			{
				onComplete?.Invoke(result);
			});
		}
		
		public static void WriteAllTextAsync (string filePath, string data, Action<WriteResult> onComplete = null)
		{
			WriteAllTextAsync(filePath, data, Encoding.UTF8, onComplete);
		}

		public static async void WriteAllTextAsync (string filePath, string data, Encoding encode, Action<WriteResult> onComplete = null)
		{
			lock (WriteBuffer)
			{
				if (WriteBuffer.ContainsKey(filePath))
				{
					throw new Exception($"Writing same file again: {filePath}");
				}
				else
				{
					WriteBuffer.Add(filePath, data);
				}
			}
			
			FileStream sourceStream = null;
			var result = new WriteResult();
			
			try
			{
				var bData = encode.GetBytes(data);
				sourceStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
				await sourceStream.WriteAsync(bData, 0, data.Length, CancellationToken.None);
				result.Success = true;
			}
			catch (Exception e)
			{
				result.Error = e;
				LogError(e, filePath);
			}
			finally
			{
				sourceStream?.Close();
			}
			
			lock (WriteBuffer)
			{
				WriteBuffer.Remove(filePath);
			}
				
			Loom.QueueOnMainThread(() =>
			{
				onComplete?.Invoke(result);
			});
		}

		private static void LogError(Exception e, string filePath)
		{
			EventTrack.LogError(e);
		}

		public struct WriteResult
		{
			public bool Success;
			public Exception Error;
		}
	}
}