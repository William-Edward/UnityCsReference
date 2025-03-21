// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEditor.PackageManager.Requests;

namespace UnityEditor.PackageManager.UI.Internal
{
    [Serializable]
    internal abstract class UpmBaseOperation : IOperation
    {
        public abstract event Action<IOperation, UIError> onOperationError;
        public abstract event Action<IOperation> onOperationSuccess;
        public abstract event Action<IOperation> onOperationFinalized;
        public abstract event Action<IOperation> onOperationProgress;

        [SerializeField]
        protected string m_PackageName = string.Empty;
        public string packageName
        {
            get
            {
                if (!string.IsNullOrEmpty(m_PackageName))
                    return m_PackageName;
                if (!string.IsNullOrEmpty(m_PackageId))
                    return m_PackageId.Split(new[] { '@' }, 2)[0];
                return string.Empty;
            }
        }

        [SerializeField]
        protected string m_PackageId = string.Empty;
        public string packageId { get { return m_PackageId; } }

        [SerializeField]
        protected string m_PackageUniqueId = string.Empty;
        public string packageUniqueId { get { return m_PackageUniqueId; } }
        public string versionUniqueId { get { return packageId; } }

        [SerializeField]
        protected long m_Timestamp = 0;
        public long timestamp { get { return m_Timestamp; } }

        [SerializeField]
        protected long m_LastSuccessTimestamp = 0;
        public long lastSuccessTimestamp { get { return m_LastSuccessTimestamp; } }

        [SerializeField]
        protected bool m_OfflineMode = false;
        public bool isOfflineMode { get { return m_OfflineMode; } }

        public abstract bool isInProgress { get; }

        public bool isProgressVisible => false;

        public bool isProgressTrackable => false;

        public float progressPercentage => 0;

        public UIError error { get; protected set; }        // Keep last error

        public abstract RefreshOptions refreshOptions { get; }

        [NonSerialized]
        protected ClientProxy m_ClientProxy;
        [NonSerialized]
        protected ApplicationProxy m_ApplicationProxy;
        public void ResolveDependencies(ClientProxy clientProxy, ApplicationProxy applicationProxy)
        {
            m_ClientProxy = clientProxy;
            m_ApplicationProxy = applicationProxy;
        }
    }

    [Serializable]
    internal abstract class UpmBaseOperation<T> : UpmBaseOperation where T : Request
    {
        public override event Action<IOperation, UIError> onOperationError = delegate {};
        public override event Action<IOperation> onOperationFinalized = delegate {};
        public override event Action<IOperation> onOperationSuccess = delegate {};
        public override event Action<IOperation> onOperationProgress = delegate {};
        public Action<T> onProcessResult = delegate {};

        [SerializeField]
        protected T m_Request;
        [SerializeField]
        protected bool m_IsCompleted;
        public override bool isInProgress { get { return m_Request != null && m_Request.Id != 0 && !m_IsCompleted; } }

        protected abstract T CreateRequest();

        protected void Start()
        {
            if (isInProgress)
            {
                Debug.LogError(L10n.Tr("[Package Manager Window] Unable to start the operation again while it's in progress. " +
                    "Please cancel the operation before re-start or wait until the operation is completed."));
                return;
            }

            if (!isOfflineMode)
                m_Timestamp = DateTime.Now.Ticks;
            // Usually the timestamp for an offline operation is the last success timestamp of its online equivalence (to indicate the freshness of the data)
            // But in the rare case where we start an offline operation before an online one, we use the start timestamp of the editor instead of 0,
            // because we consider a `0` refresh timestamp as `not initialized`/`no refreshes have been done`.
            else if (m_Timestamp == 0)
                m_Timestamp = DateTime.Now.Ticks - (long)(EditorApplication.timeSinceStartup * TimeSpan.TicksPerSecond);

            error = null;

            if (!m_ApplicationProxy.isUpmRunning)
            {
                EditorApplication.delayCall += () =>
                {
                    OnError(new UIError(UIErrorCode.UpmError, "UPM server is not running"));
                    Cancel();
                };
                return;
            }

            try
            {
                m_Request = CreateRequest();
            }
            catch (ArgumentException e)
            {
                OnError(new UIError(UIErrorCode.UpmError, e.Message));
                return;
            }
            m_IsCompleted = false;
            EditorApplication.update += Progress;
        }

        public void Cancel()
        {
            OnFinalize();
            m_Request = null;
        }

        // Common progress code for all classes
        protected void Progress()
        {
            m_IsCompleted = m_Request.IsCompleted;
            if (m_IsCompleted)
            {
                if (m_Request.Status == StatusCode.Success)
                    OnSuccess();
                else if (m_Request.Status >= StatusCode.Failure)
                    OnError(new UIError((UIErrorCode)m_Request.Error.errorCode, m_Request.Error.message, UIError.Attribute.IsDetailInConsole));
                else
                    Debug.LogError(string.Format(L10n.Tr("[Package Manager Window] Unsupported progress state {0}."), m_Request.Status));
                OnFinalize();
            }
        }

        public void RestoreProgress()
        {
            if (isInProgress)
                EditorApplication.update += Progress;
        }

        private void OnError(UIError error)
        {
            this.error = error;
            var message = L10n.Tr("Cannot perform upm operation");
            message += string.IsNullOrEmpty(error.message) ? "." : $": {error.message} [{error.errorCode}].";

            Debug.LogError($"{L10n.Tr("[Package Manager Window]")} {message}");
            onOperationError?.Invoke(this, error);
        }

        private void OnSuccess()
        {
            onProcessResult?.Invoke(m_Request);
            m_LastSuccessTimestamp = m_Timestamp;
            onOperationSuccess?.Invoke(this);
        }

        private void OnFinalize()
        {
            EditorApplication.update -= Progress;
            onOperationFinalized?.Invoke(this);

            onOperationError = delegate {};
            onOperationFinalized = delegate {};
            onOperationSuccess = delegate {};
            onOperationProgress = delegate {};
            onProcessResult = delegate {};
        }
    }
}
