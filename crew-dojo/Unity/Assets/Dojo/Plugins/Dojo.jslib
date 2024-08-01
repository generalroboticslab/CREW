mergeInto(LibraryManager.library, {

  RegisterUnload: function (objectName) {
    let realName = UTF8ToString(objectName);
    window.onbeforeunload = () => {
        window.unityInstance.SendMessage(realName, "OnApplicationQuit");
        return "Are you sure to leave this page?";
    }
  },

});
