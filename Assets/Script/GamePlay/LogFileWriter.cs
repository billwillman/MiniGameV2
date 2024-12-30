using System;
using System.IO;
using UnityEngine;
using Utils;

namespace SOC.GamePlay
{
    public class LogFileWriter : DisposeObject, ILogHandler
    {
        public bool IsLogWriteAsync = true;

        string GetSaveFileName(string localFileName) {
            string dateTimeStr = DateTime.Now.ToString().Replace("-", "_").Replace("/", "_").Replace(":", "_");
            string ret = localFileName;
            if (GameStart.IsDS) {
                if (!Directory.Exists("log"))
                    Directory.CreateDirectory("log");
                ret = string.Format("log/{0}_{1}.log", ret, dateTimeStr);
            } else if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.LinuxPlayer) {
                if (!Directory.Exists("log"))
                    Directory.CreateDirectory("log");
                ret = string.Format("log/{0}_{1}.log", ret, dateTimeStr);
            } else
                ret = string.Format("{0}/{1}_{2}.log", Application.persistentDataPath, ret, dateTimeStr);
            return ret;
        }

        public LogFileWriter(string saveFileName, bool IsOutOldHandle) {
            if (string.IsNullOrEmpty(saveFileName))
                saveFileName = "logWriter";
            saveFileName = GetSaveFileName(saveFileName);
            try {
                m_FileStream = new FileStream(saveFileName, FileMode.Create, FileAccess.Write);
                m_OldLogHandle = Debug.unityLogger.logHandler;
                Debug.unityLogger.logHandler = this;
                m_IsOutOldHandle = IsOutOldHandle;
            } catch (Exception e) {
                Debug.LogError(e.ToString());
            }
        }

        protected override void OnFree(bool isManual) {
            if (m_FileStream != null) {
                m_FileStream.Dispose();
                m_FileStream = null;
            }
            if (m_OldLogHandle != null) {
                Debug.unityLogger.logHandler = m_OldLogHandle;
                m_OldLogHandle = null;
            }
        }

        // 如果后面日志需要优化采用 Memory<char> RingBuffer + 异步的策略
        // 目前暂时采用直接异步
        protected void WriteLog(string log) {
            if (string.IsNullOrEmpty(log) || m_FileStream == null)
                return;
            log = string.Format("[{0}] {1}\n", DateTime.Now.ToString(), log);
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(log);
            if (IsLogWriteAsync) {
                m_FileStream.WriteAsync(buffer);
                m_FileStream.FlushAsync();
            } else {
                m_FileStream.Write(buffer);
                m_FileStream.Flush();
            }
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) {
            if (m_IsOutOldHandle && m_OldLogHandle != null) {
                m_OldLogHandle.LogFormat(logType, context, format, args);
            }
            if (m_FileStream != null) {
                string log = string.Format("LOG: [{0}] {1}==>> {2}", Enum.GetName(GetLogType(), logType), context != null ? context.name : string.Empty, string.Format(format, args));
                WriteLog(log);
            }
        }

        public void LogException(Exception exception, UnityEngine.Object context) {
            if (m_IsOutOldHandle && m_OldLogHandle != null) {
                m_OldLogHandle.LogException(exception, context);
            }
            if (m_FileStream != null) {
                string log = string.Format("EXCEPTION: {0}==>> {1}", context != null ? context.name : string.Empty, exception != null ? exception.ToString() : string.Empty);
                WriteLog(log);
            }
        }

        protected Type GetLogType() {
            if (m_LogType == null)
                m_LogType = typeof(LogType);
            return m_LogType;
        }

        private Type m_LogType = null;
        private FileStream m_FileStream = null;
        private ILogHandler m_OldLogHandle = null;
        private bool m_IsOutOldHandle = true;
    }
}