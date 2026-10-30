// ブラウザ上でローカルファイルを開く/ダウンロードさせるためのUnity WebGLプラグイン
// ファイルはサーバに送信せず、すべてブラウザ内で処理する
var VrmWebIO = {
    $VrmWebIOState: {
        gameObjectName: null,
        input: null,
        pointerDown: false,
        pendingOpen: false,
        data: null,
        name: null
    },

    VrmWebIO_Initialize: function (gameObjectNamePtr) {
        var s = VrmWebIOState;
        s.gameObjectName = UTF8ToString(gameObjectNamePtr);
        if (s.input) return;

        var notify = function (file) {
            var reader = new FileReader();
            reader.onload = function () {
                s.data = new Uint8Array(reader.result);
                s.name = file.name;
                SendMessage(s.gameObjectName, "OnWebFileOpened", file.name);
            };
            reader.onerror = function () {
                SendMessage(s.gameObjectName, "OnWebFileError", String(reader.error));
            };
            reader.readAsArrayBuffer(file);
        };

        var input = document.createElement("input");
        input.type = "file";
        input.accept = ".vrm,.glb";
        input.style.display = "none";
        input.addEventListener("change", function () {
            if (input.files && input.files.length > 0) notify(input.files[0]);
            // 同じファイルを続けて選択しても change が発火するようにする
            input.value = "";
        });
        input.addEventListener("cancel", function () {
            SendMessage(s.gameObjectName, "OnWebFileCanceled", "");
        });
        document.body.appendChild(input);
        s.input = input;

        // ブラウザはユーザー操作中でないとファイル選択ダイアログを開けない
        // UnityのButtonは次フレームでクリックを処理するため、
        // ポインタが離された瞬間(実イベント内)にダイアログを開く
        document.addEventListener("pointerdown", function () { s.pointerDown = true; }, true);
        var onRelease = function () {
            s.pointerDown = false;
            if (s.pendingOpen) {
                s.pendingOpen = false;
                input.click();
            }
        };
        document.addEventListener("pointerup", onRelease, true);
        document.addEventListener("pointercancel", function () { s.pointerDown = false; }, true);

        // ドラッグ&ドロップでも開けるようにする
        document.addEventListener("dragover", function (e) { e.preventDefault(); });
        document.addEventListener("drop", function (e) {
            e.preventDefault();
            if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                notify(e.dataTransfer.files[0]);
            }
        });
    },

    // Buttonのクリック処理(次フレーム)から呼ばれる
    VrmWebIO_OpenFileDialog: function () {
        var s = VrmWebIOState;
        if (!s.input) return;
        if (s.pointerDown) {
            // まだ押されている最中 -> 離された時に開く
            s.pendingOpen = true;
        } else {
            // 既に離されている -> ブラウザのユーザー操作猶予内であればそのまま開く
            s.input.click();
        }
    },

    VrmWebIO_GetDataSize: function () {
        var s = VrmWebIOState;
        return s.data ? s.data.length : 0;
    },

    // 読み込み済みのファイル内容をUnity側のバッファにコピーして、JS側の参照は破棄する
    VrmWebIO_CopyData: function (ptr) {
        var s = VrmWebIOState;
        if (!s.data) return;
        HEAPU8.set(s.data, ptr);
        s.data = null;
    },

    VrmWebIO_Download: function (ptr, size, fileNamePtr) {
        var fileName = UTF8ToString(fileNamePtr);
        var blob = new Blob([HEAPU8.slice(ptr, ptr + size)], { type: "application/octet-stream" });
        var url = URL.createObjectURL(blob);
        var a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        a.style.display = "none";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
    }
};

autoAddDeps(VrmWebIO, '$VrmWebIOState');
mergeInto(LibraryManager.library, VrmWebIO);
