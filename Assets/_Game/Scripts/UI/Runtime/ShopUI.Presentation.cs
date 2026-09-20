using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 상점의 대사 반응·이미지·입장 연출·음악 복구를 담당합니다. 수명 주기는 ShopUI.cs에서 관리합니다.
public sealed partial class ShopUI
{
    private readonly List<KeyValuePair<string, Action>> _reactionSubscriptions = new List<KeyValuePair<string, Action>>();
    private CanvasGroup _contentGroup;
    private Image _entryBlack;
    private Tween _entryFadeTween;
    private bool _isOpening;
    private AudioManager _shopAudio;
    private AudioClip _shopMusic;
    private ulong _shopMusicRequestVersion;
    private BgmPlaybackSnapshot _previousMusic;
    private float _musicFadeDuration;
    private IDisposable _ambienceSilence;
    private Tween _shakeTween;

    private void SubscribeShopReactions()
    {
        UnsubscribeShopReactions();
        foreach (ShopPresentationReaction reaction in _activeShop.Reactions)
        {
            Action callback = () => ApplyShopReaction(reaction);
            EventManager.Subscribe(reaction.EventId, callback);
            _reactionSubscriptions.Add(new KeyValuePair<string, Action>(reaction.EventId, callback));
        }
    }

    private void UnsubscribeShopReactions()
    {
        foreach (KeyValuePair<string, Action> subscription in _reactionSubscriptions)
            EventManager.Unsubscribe(subscription.Key, subscription.Value);
        _reactionSubscriptions.Clear();
    }

    private void ApplyShopReaction(ShopPresentationReaction reaction)
    {
        // 현재 상점이 시작한 대화 재생 중에만 반응합니다. 다른 대화/상점의 이벤트는 받지 않습니다.
        if (!_visible || !_suspendInput || _dialogueOwner == null || !_dialogueOwner.IsPlaying
            || _dialogueGeneration == 0 || _dialogueOwner.PlaybackGeneration != _dialogueGeneration)
            return;
        if (reaction.Portrait != null)
            SetVendorPortrait(reaction.Portrait);
        if (reaction.Shake)
            PlayVendorShake(reaction.ShakeDuration, reaction.ShakeStrength);
    }

    private void SetVendorPortrait(Sprite portrait)
    {
        if (_vendorImage == null)
            return;
        _vendorImage.sprite = portrait != null ? portrait : _activeShop != null ? _activeShop.VendorPortrait : null;
        _vendorImage.gameObject.SetActive(_vendorImage.sprite != null);
    }

    private void PlayVendorShake(float duration, float multiplier)
    {
        if (!_visible || !_suspendInput || _vendorImage == null || !_vendorImage.gameObject.activeSelf)
            return;

        StopVendorShake();
        float strength = GameConfigManager.Instance != null
            ? GameConfigPolicy.NormalizeUnit(GameConfigManager.Instance.ScreenShake, GameConfigManager.DefaultScreenShake)
            : GameConfigManager.DefaultScreenShake;
        strength *= multiplier;
        if (strength <= 0f || duration <= 0f)
            return;

        // UI 이미지 자체를 흔들어 FixedViewport 출력 카메라의 위치/다른 연출에는 관여하지 않습니다.
        _shakeTween = _vendorImage.rectTransform
            .DOShakeAnchorPos(duration, new Vector2(7f, 3f) * strength, 20, 90f, false, true)
            .SetUpdate(true)
            .OnComplete(StopVendorShake);
    }

    private void StopVendorShake()
    {
        _shakeTween?.Kill(false);
        _shakeTween = null;
        if (_vendorImage != null)
            _vendorImage.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void BeginEntrance()
    {
        StopEntrance();
        _ambienceSilence?.Dispose();
        _ambienceSilence = AudioManager.Instance?.SuppressAmbience();
        StartShopMusic();
        float fadeOut = _activeShop.EntryFadeOutDuration;
        float fadeIn = _activeShop.EntryFadeInDuration;
        if (fadeOut <= 0f && fadeIn <= 0f)
        {
            _contentGroup.alpha = 1f;
            return;
        }

        _isOpening = true;
        _contentGroup.alpha = 0f;
        _entryBlack.color = new Color(0f, 0f, 0f, 0f);
        _entryBlack.gameObject.SetActive(true);
        // 기존 맵 → 암전 → 상점. 시간 배율과 무관하며 중단 시 지연 콜백을 남기지 않습니다.
        _entryFadeTween = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_entryBlack.DOFade(1f, fadeOut).SetEase(Ease.Linear))
            .AppendCallback(() => _contentGroup.alpha = 1f)
            .Append(_entryBlack.DOFade(0f, fadeIn).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                _entryFadeTween = null;
                _isOpening = false;
                _entryBlack.gameObject.SetActive(false);
                _submitFrame = Time.frameCount;
            });
    }

    private void StopEntrance()
    {
        _entryFadeTween?.Kill(false);
        _entryFadeTween = null;
        _isOpening = false;
        if (_entryBlack != null)
            _entryBlack.gameObject.SetActive(false);
    }

    private void StartShopMusic()
    {
        AudioManager audio = AudioManager.Instance;
        AudioClip clip = _activeShop.BackgroundMusic;
        if (audio == null || clip == null || audio.RequestedBgmClip == clip)
            return;

        _shopAudio = audio;
        _shopMusic = clip;
        _previousMusic = audio.CaptureBgmPlayback();
        _musicFadeDuration = _activeShop.MusicFadeDuration;
        audio.CrossFadeBGM(clip, _musicFadeDuration);
        _shopMusicRequestVersion = audio.BgmRequestVersion;
    }

    private void RestoreShopMusic(bool restorePrevious = true)
    {
        // 다른 연출/새 맵이 음악을 바꿨다면 이전 맵 음악으로 덮어쓰지 않습니다.
        if (_shopAudio != null && _shopAudio == AudioManager.Instance
            && _shopAudio.BgmRequestVersion == _shopMusicRequestVersion
            && _shopAudio.RequestedBgmClip == _shopMusic && _shopMusic != null)
        {
            if (restorePrevious)
                _shopAudio.RestoreBgmPlayback(_previousMusic, _musicFadeDuration);
            else
                _shopAudio.StopBGM(_musicFadeDuration);
        }
        _shopAudio = null;
        _shopMusic = null;
        _shopMusicRequestVersion = 0;
        _previousMusic = BgmPlaybackSnapshot.Stopped;
    }
}
