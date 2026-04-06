mergeInto(LibraryManager.library, {
  _oAuth: null,
  _getUnityInstance: function () {
    if (
      window.unityInstance &&
      typeof window.unityInstance.SendMessage === "function"
    ) {
      return window.unityInstance;
    }
    if (typeof unityInstance !== "undefined" && unityInstance) {
      return unityInstance;
    }
    return null;
  },

  oAuthInit: function () {
    this._oAuth =
      this._oAuth ||
      new (function () {
        this.popup = null;
        this.previousUrl = null;

        this.init = () => {
          const cleanUp = () => {
            this.popup && this.popup.close();
            window.removeEventListener("message", messageHandler);
          };

          const messageHandler = (event) => {
            if (!event.data) return;

            if (
              event.data.type === "PRIVY_OAUTH_RESPONSE" &&
              event.data.stateCode &&
              event.data.authorizationCode
            ) {
              const unity = LibraryManager.library._getUnityInstance();
              if (!unity) {
                console.warn("Unity instance not ready; dropping OAuth response.");
                return;
              }
              unity.SendMessage(
                "WebGLPopupMessageInterceptor",
                "SignedIn",
                JSON.stringify({
                  stateCode: event.data.stateCode,
                  authorizationCode: event.data.authorizationCode,
                }),
              );
              cleanUp();
            }

            if (event.data.type === "PRIVY_OAUTH_ERROR") {
              const unity = LibraryManager.library._getUnityInstance();
              if (!unity) {
                console.warn("Unity instance not ready; dropping OAuth error.");
                return;
              }
              unity.SendMessage(
                "WebGLPopupMessageInterceptor",
                "SignInFailed",
              );
              cleanUp();
            }
          };

          window.addEventListener("message", messageHandler);
        };

        this.signIn = async (url) => {
          const name = "privyOAuthPopup";
          const strWindowFeatures =
            "toolbar=no, menubar=no, width=600, height=700, top=100, left=100";

          if (this.popup === null || this.popup.closed) {
            this.popup = window.open(url, name, strWindowFeatures);
          } else if (this.previousUrl !== url) {
            this.popup = window.open(url, name, strWindowFeatures);
            this.popup.focus();
          } else {
            this.popup.focus();
          }

          this.previousUrl = url;
        };
      })();

    this._oAuth.init();
  },

  oAuthSignIn: async function (oauthUrl) {
    oauthUrl = UTF8ToString(oauthUrl);
    this._oAuth.signIn(oauthUrl);
  },
});
