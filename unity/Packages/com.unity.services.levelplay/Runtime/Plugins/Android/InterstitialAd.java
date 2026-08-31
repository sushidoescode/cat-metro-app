package com.ironsource.unity.androidbridge;

import android.app.Activity;
import com.unity3d.mediation.LevelPlayAdError;
import com.unity3d.mediation.LevelPlayAdInfo;
import com.unity3d.mediation.impression.LevelPlayImpressionDataListener;
import com.unity3d.mediation.interstitial.LevelPlayInterstitialAd;
import com.unity3d.mediation.interstitial.LevelPlayInterstitialAd.Config;
import com.unity3d.mediation.interstitial.LevelPlayInterstitialAdListener;
import com.unity3d.player.UnityPlayer;

public class InterstitialAd {
      Activity mActivity;
      LevelPlayInterstitialAd mInterstitialAd;

      public InterstitialAd(String adUnitId, Config config, IUnityInterstitialAdListener interstitialAdListener) {
         this.mActivity = UnityPlayer.currentActivity;
         this.mInterstitialAd = new LevelPlayInterstitialAd(adUnitId, config);
         setupInterstitialListener(interstitialAdListener);
      }

      private void setupInterstitialListener(IUnityInterstitialAdListener interstitialAdListener) {
         // NOTE: the LevelPlay SDK delivers these callbacks on the Android UI thread. Invoking the
         // IUnityInterstitialAdListener proxy crosses into the Unity scripting runtime, which can
         // block for an unbounded time (GC / thread attach), so the UI thread must not do it. Values
         // are stringifies on the delivering thread, then the proxy call is handed to the background executor.
         this.mInterstitialAd.setListener(new LevelPlayInterstitialAdListener() {
            @Override
            public void onAdLoaded(LevelPlayAdInfo levelPlayAdInfo) {
               if (interstitialAdListener != null) {
                  final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdLoaded(adInfo));
               }
            }

            @Override
            public void onAdLoadFailed(LevelPlayAdError levelPlayAdError) {
               if (interstitialAdListener != null) {
                  final String error = LevelPlayUtils.adErrorToString(levelPlayAdError);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdLoadFailed(error));
               }
            }

            @Override
            public void onAdDisplayed(LevelPlayAdInfo levelPlayAdInfo) {
               if (interstitialAdListener != null) {
                  final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdDisplayed(adInfo));
               }
            }

            @Override
            public void onAdClosed(LevelPlayAdInfo levelPlayAdInfo) {
               if (interstitialAdListener != null) {
                  final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdClosed(adInfo));
               }
            }

            @Override
            public void onAdClicked(LevelPlayAdInfo levelPlayAdInfo) {
               if (interstitialAdListener != null) {
                  final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdClicked(adInfo));
               }
            }

            @Override
            public void onAdDisplayFailed(LevelPlayAdError levelPlayAdError, LevelPlayAdInfo levelPlayAdInfo) {
               if (interstitialAdListener != null) {
                  final String error = LevelPlayUtils.adErrorToString(levelPlayAdError);
                  final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdDisplayFailed(error, adInfo));
               }
            }

            @Override
            public void onAdInfoChanged(LevelPlayAdInfo levelPlayAdInfo) {
               if (interstitialAdListener != null) {
                  final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                  AndroidBridgeUtilities.postBackgroundTask(() -> interstitialAdListener.onAdInfoChanged(adInfo));
               }
            }
         });
      }

      public void loadAd() {
        this.mInterstitialAd.loadAd();
      }

      public void showAd(String placementName) {
        this.mInterstitialAd.showAd(mActivity, placementName);
      }

      public boolean isAdReady() {
        return this.mInterstitialAd.isAdReady();
      }

      public static boolean isPlacementCapped(String placementName) {
          return LevelPlayInterstitialAd.isPlacementCapped(placementName);
      }

      public String getAdId() {
          return this.mInterstitialAd.getAdId();
      }

      public void setImpressionDataListener(UnityImpressionDataListener listener) {
          LevelPlayImpressionDataListener nativeListener = listener == null ? null
              : impressionData -> AndroidBridgeUtilities.postBackgroundTask(
                  () -> listener.onImpressionSuccess(AndroidBridgeUtilities.getImpressionDataString(impressionData)));
          this.mInterstitialAd.setImpressionDataListener(nativeListener);
      }

      public static class ConfigBuilder {
        private final Config.Builder builder = new Config.Builder();

        public void setBidFloor(double bidFloor) {
            builder.setBidFloor(bidFloor);
        }

        public Config build() {
            return builder.build();
        }
      }
}
