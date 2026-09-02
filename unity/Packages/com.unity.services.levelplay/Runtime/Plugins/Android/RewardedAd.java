package com.ironsource.unity.androidbridge;

import android.app.Activity;
import com.unity3d.mediation.LevelPlayAdError;
import com.unity3d.mediation.LevelPlayAdInfo;
import com.unity3d.mediation.impression.LevelPlayImpressionDataListener;
import com.unity3d.mediation.rewarded.LevelPlayReward;
import com.unity3d.mediation.rewarded.LevelPlayRewardedAd;
import com.unity3d.mediation.rewarded.LevelPlayRewardedAd.Config;
import com.unity3d.mediation.rewarded.LevelPlayRewardedAdListener;
import com.unity3d.player.UnityPlayer;

public class RewardedAd {
   Activity mActivity;

   LevelPlayRewardedAd mRewardedAd;

   public RewardedAd(String adUnitId, Config config, IUnityRewardedAdListener rewardedAdListener) {
      this.mActivity = UnityPlayer.currentActivity;

      this.mRewardedAd = new LevelPlayRewardedAd(adUnitId, config);
      setupRewardedListener(rewardedAdListener);
   }

   private void setupRewardedListener(IUnityRewardedAdListener rewardedAdListener) {
      // NOTE: the LevelPlay SDK delivers these callbacks on the Android UI thread. Invoking the
      // IUnityRewardedAdListener proxy crosses into the Unity scripting runtime, which can block for
      // an unbounded time (GC / thread attach), so the UI thread must not do it. Values are
      // stringifies on the delivering thread, then the proxy call is handed to the background executor.
      this.mRewardedAd.setListener(new LevelPlayRewardedAdListener() {
         @Override
         public void onAdLoaded(LevelPlayAdInfo levelPlayAdInfo) {
            if (rewardedAdListener != null) {
               final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
               AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdLoaded(adInfo));
            }
         }

         @Override
         public void onAdLoadFailed(LevelPlayAdError levelPlayAdError) {
            if (rewardedAdListener != null) {
               final String error = LevelPlayUtils.adErrorToString(levelPlayAdError);
               AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdLoadFailed(error));
            }
         }

         @Override
         public void onAdDisplayed(LevelPlayAdInfo levelPlayAdInfo) {
             if (rewardedAdListener != null) {
                final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
                AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdDisplayed(adInfo));
             }
         }

         @Override
         public void onAdRewarded(LevelPlayReward levelPlayReward,
             LevelPlayAdInfo levelPlayAdInfo) {
            if (rewardedAdListener != null) {
               final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
               final String rewardName = levelPlayReward.getName();
               final int rewardAmount = levelPlayReward.getAmount();
               AndroidBridgeUtilities.postBackgroundTask(
                   () -> rewardedAdListener.onAdRewarded(adInfo, rewardName, rewardAmount));
            }
         }

         @Override
         public void onAdDisplayFailed(LevelPlayAdError levelPlayAdError, LevelPlayAdInfo levelPlayAdInfo) {
            if (rewardedAdListener != null) {
               final String error = LevelPlayUtils.adErrorToString(levelPlayAdError);
               final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
               AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdDisplayFailed(error, adInfo));
            }
         }

         @Override
         public void onAdClosed(LevelPlayAdInfo levelPlayAdInfo) {
            if (rewardedAdListener != null) {
               final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
               AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdClosed(adInfo));
            }
         }

         @Override
         public void onAdInfoChanged(LevelPlayAdInfo levelPlayAdInfo) {
            if (rewardedAdListener != null) {
               final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
               AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdInfoChanged(adInfo));
            }
         }

         @Override
         public void onAdClicked(LevelPlayAdInfo levelPlayAdInfo) {
            if (rewardedAdListener != null) {
               final String adInfo = LevelPlayUtils.adInfoToString(levelPlayAdInfo);
               AndroidBridgeUtilities.postBackgroundTask(() -> rewardedAdListener.onAdClicked(adInfo));
            }
         }
      });
   }

   public void loadAd(){
      this.mRewardedAd.loadAd();
   }

   public void showAd(String placementName) {
      this.mRewardedAd.showAd(mActivity, placementName);
   }

   public boolean isAdReady() {
      return this.mRewardedAd.isAdReady();
   }

   public static boolean isPlacementCapped(String placementName) {
      return LevelPlayRewardedAd.isPlacementCapped(placementName);
   }

   public String getAdId() {
      return this.mRewardedAd.getAdId();
   }

   public LevelPlayReward getReward(String placement) {
      return this.mRewardedAd.getReward(placement);
   }

   public void setImpressionDataListener(UnityImpressionDataListener listener) {
      LevelPlayImpressionDataListener nativeListener = listener == null ? null
          : impressionData -> AndroidBridgeUtilities.postBackgroundTask(
              () -> listener.onImpressionSuccess(AndroidBridgeUtilities.getImpressionDataString(impressionData)));
      this.mRewardedAd.setImpressionDataListener(nativeListener);
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
