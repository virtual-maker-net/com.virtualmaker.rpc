// Licensed under the MIT License. See LICENSE in the project root for license information.

// Connection Sequence:
// 1. Web app starts Unity in an iframe and listens for events.
// 2. Messages are exchanged with unity-to-js and js-to-unity

const UnityRpcTransportWebGL = {
    UnityRpcTransportWebGLCreate: function(messageCb) {
        window.addEventListener('js-to-unity', (e) => {
            const bufferSize = lengthBytesUTF8(e.detail) + 1;
            const buffer = _malloc(bufferSize);
            stringToUTF8(e.detail, buffer, bufferSize);

            try {
                Module.dynCall_vi(messageCb, buffer);
            } finally {
                _free(buffer);
            }
        });

        window.dispatchEvent(new CustomEvent('unity-to-js', { detail: 'unity-ready' }));
    },

    UnityRpcTransportWebGLSend: function(data) {
        window.dispatchEvent(new CustomEvent('unity-to-js', { detail: UTF8ToString(data) }));
    }
}

mergeInto(LibraryManager.library, UnityRpcTransportWebGL);