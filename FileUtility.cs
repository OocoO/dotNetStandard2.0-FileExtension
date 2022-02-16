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
		private static readonly Dictionary<string, Task> s_fileTasks = new Dictionary<string, Task>();

		public static void ReadAllTextAsyncWithCallback(string filePath, Action<string> onComplete)
		{
			ReadALlTextAsyncWithCallback(filePath, Encoding.UTF8, onComplete);
		}

		public static void ReadALlTextAsyncWithCallback(string filePath, Encoding encoding, Action<string> onComplete)
		{
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
			Task<string> task = null;
			string FileProcess()
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
					LogError(e, filePath);
				}
				finally
				{
					sourceStream?.Close();
				}

				// ReSharper disable once AccessToModifiedClosure
				OnTaskComplete(filePath, task);

				return value;
			}
			
			return OnTaskCreate(filePath, out task, FileProcess);
		}


		public static void ReadAllBytesAsyncWithCallback(string filePath, Action<byte[]> onComplete)
		{
			var task = ReadAllBytesAsync(filePath);
			task.ContinueWith(x =>
			{
				Loom.QueueOnMainThread(() => onComplete.Invoke(x.Result));
			});
		}

		public static Task<byte[]> ReadAllBytesAsync(string filePath)
		{
			Task<byte[]> task = null;
			byte[] FileProcess()
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
					LogError(e, filePath);
				}
				finally
				{
					sourceStream?.Close();
				}
				
				// ReSharper disable once AccessToModifiedClosure
				OnTaskComplete(filePath, task);

				return value;
			}
			
			return OnTaskCreate(filePath, out task, FileProcess);;
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
			Task<WriteResult> task = null;
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
					LogError(e, filePath);
				}
				finally
				{
					sourceStream?.Close();
				}

				OnTaskComplete(filePath, task);

				return result;
			}


			return OnTaskCreate(filePath, out task, FileProcess);
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
			Task<WriteResult> task = null;
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
					LogError(e, filePath);
				}
				finally
				{
					sourceStream?.Close();
				}

				// ReSharper disable once AccessToModifiedClosure
				OnTaskComplete(filePath, task);
				
				return result;
			}

			return OnTaskCreate(filePath, out task, FileProcess);
		}

		private static void LogError(Exception e, string filePath)
		{
			Console.WriteLine(e);
		}

		public struct WriteResult
		{
			public bool Success;
			public Exception Error;
		}

		private static Task<T> OnTaskCreate<T>(string path, out Task<T> task, Func<T> fileProcess)
		{
			lock (s_fileTasks)
			{
				if (s_fileTasks.TryGetValue(path, out var prv))
				{
					task = prv.ContinueWith(x => fileProcess());
				}
				else
				{
					task = Task.Run(fileProcess);
				}
				
				s_fileTasks[path] = task;
			}

			var outTask = task;
			return outTask;
		}
		
		private static void OnTaskComplete(string path, Task task)
		{
			lock (s_fileTasks)
			{
				if (s_fileTasks.ContainsKey(path) && s_fileTasks[path] == task)
				{
					s_fileTasks.Remove(path);
				}
			}
		}
	}
}