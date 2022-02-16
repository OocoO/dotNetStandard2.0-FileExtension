using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Carotaa.Utility
{
	public static class FileUtility
	{
		// Used to solve some Stupid Condition: Writing and reading the same file.
		private static readonly Dictionary<string, WriteProcess> s_writeBuffer = new Dictionary<string, WriteProcess>();
		
		private static Dictionary<string, WriteProcess> Buffer
		{
			get
			{
				lock (s_writeBuffer)
				{
					return s_writeBuffer;
				}
			}
		}

		public static void ReadAllTextAsyncWithCallback(string filePath, Action<string> onComplete)
		{
			ReadALlTextAsyncWithCallback(filePath, Encoding.UTF8, onComplete);
		}

		public static void ReadALlTextAsyncWithCallback(string filePath, Encoding encoding, Action<string> onComplete)
		{
			if (Buffer.ContainsKey(filePath))
			{
				var buffer = Buffer[filePath].Text;
				onComplete.Invoke(buffer);
				return;
			}
			
			var task = ReadAllTextAsync(filePath, encoding);

			task.ContinueWith(prv =>
			{
				Loom.QueueOnMainThread(() => onComplete.Invoke(prv.Result));
			});
		}

		public static Task<string> ReadAllTextAsync(string filePath)
		{
			return ReadAllTextAsync(filePath, Encoding.UTF8);
		}

		public static Task<string> ReadAllTextAsync(string filePath, Encoding encoding)
		{
			var task = Task.Run(() =>
			{
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
					while ((numRead = sourceStream.Read(buffer, 0, buffer.Length)) != 0)
					{
						string text = encoding.GetString(buffer, 0, numRead);
						sb.Append(text);
					}

					value = sb.ToString();
				}
				catch (Exception e)
				{
					LogError(e);
				}
				finally
				{
					sourceStream?.Close();
				}

				return value;
			});

			return task;
		}


		public static void ReadAllBytesAsyncWithCallback(string filePath, Action<byte[]> onComplete)
		{
			if (Buffer.ContainsKey(filePath))
			{
				var buffer = Buffer[filePath].Bytes;
				onComplete.Invoke(buffer);
				return;
			}
			
			var task = ReadAllBytesAsync(filePath);
			task.ContinueWith(x =>
			{
				Loom.QueueOnMainThread(() => onComplete.Invoke(x.Result));
			});
		}

		public static Task<byte[]> ReadAllBytesAsync(string filePath)
		{
			var task = Task.Run(() =>
			{
				FileStream sourceStream = null;
				byte[] value = null;
				try
				{
					sourceStream = new FileStream(filePath,
						FileMode.Open, FileAccess.Read, FileShare.Read,
						bufferSize: 4096, useAsync: true);

					value = new byte[sourceStream.Length];
					sourceStream.Read(value, 0, (int)sourceStream.Length);
				}
				catch (Exception e)
				{
					LogError(e);
				}
				finally
				{
					sourceStream?.Close();
				}

				return value;
			});

			return task;
		}

		public static void WriteAllBytesAsyncWithCallback(string filePath, byte[] data, Action<WriteResult> onComplete)
		{
			var task = WriteAllBytesAsync(filePath, data);
			
			task.ContinueWith(prv =>
			{
				Loom.QueueOnMainThread(() => { onComplete.Invoke(prv.Result); });
			});
		}

		public static Task<WriteResult> WriteAllBytesAsync(string filePath, byte[] data)
		{
			WriteResult FileProcess()
			{
				FileStream sourceStream = null;

				var result = new WriteResult();
				try
				{
					sourceStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
					sourceStream.Write(data, 0, data.Length);
					result.Success = true;
				}
				catch (Exception e)
				{
					result.Error = e;
					LogError(e);
				}
				finally
				{
					sourceStream?.Close();
				}

				return result;
			}

			Task<WriteResult> task;
			if (Buffer.TryGetValue(filePath, out var process))
			{
				process.Bytes = data;
				task = process.Task.ContinueWith(x => FileProcess());
			} 
			else
			{
				task = Task.Run(FileProcess);
				process = new WriteProcess() {
					Bytes = data,
					Task = task,
				};
				Buffer.Add(filePath, process);
			}

			return task;
		}

		public static void WriteAllTextAsyncWithCallback(string filePath, string data, Action<WriteResult> onComplete)
		{
			WriteAllTextAsyncWithCallback(filePath, data, Encoding.UTF8, onComplete);
		}

		public static void WriteAllTextAsyncWithCallback(string filePath, string data, Encoding encode,
			Action<WriteResult> onComplete)
		{
			var task = WriteAllTextAsync(filePath, data, encode);

			task.ContinueWith(prv =>
			{
				Loom.QueueOnMainThread(() => { onComplete.Invoke(prv.Result); });
			});
		}

		public static Task<WriteResult> WriteAllTextAsync(string filePath, string data)
		{
			return WriteAllTextAsync(filePath, data, Encoding.UTF8);
		}

		public static Task<WriteResult> WriteAllTextAsync(string filePath, string data, Encoding encode)
		{
			WriteResult FileProcess()
			{
				FileStream sourceStream = null;
				var result = new WriteResult();

				try
				{
					var bData = encode.GetBytes(data);
					sourceStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
					sourceStream.Write(bData, 0, data.Length);
					result.Success = true;
				}
				catch (Exception e)
				{
					result.Error = e;
					LogError(e);
				}
				finally
				{
					sourceStream?.Close();
				}

				return result;
			}

			Task<WriteResult> task;
			if (Buffer.TryGetValue(filePath, out var process))
			{
				task = process.Task.ContinueWith(x => FileProcess());
			} 
			else
			{
				task = Task.Run(FileProcess);
				process = new WriteProcess() {
					Text = data,
					Task = task,
				};
				Buffer.Add(filePath, process);
			}

			return task;
		}

		private static void LogError(Exception e)
		{
			EventTrack.LogError(e);
		}

		public struct WriteResult
		{
			public bool Success;
			public Exception Error;
		}

		private class WriteProcess
		{
			public string Text;
			public byte[] Bytes;
			public Task Task;
		}
	}
}