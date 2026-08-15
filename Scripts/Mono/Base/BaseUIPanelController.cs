using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public abstract class BaseUIPanelController : MonoBehaviour
{
    [SerializeField] protected CanvasGroup _canvasGroup;
    [SerializeReference]
    private TempValues startValues = null;
    [SerializeField] protected bool setStartValues = true;
    [SerializeField] protected bool disableOnStart = true;
    
    public event Action OnShowStarted;
    public event Action OnShowEnded;
    public event Action OnHideStarted;
    public event Action OnHideEnded;
    
    public RectTransform RectTransform => transform as RectTransform;
    public CanvasGroup CanvasGroup => _canvasGroup;
    protected List<IAnimationHandler> _showHandlers;
    protected List<IAnimationHandler> _hideHandlers;

    protected Sequence CurrentSequence { get; set; }
    public bool IsAnimated => CurrentSequence != null && CurrentSequence.IsActive() && CurrentSequence.IsPlaying();
    
    private bool _isInitialized = false;
    protected virtual void Awake()
    {
        StoreInitialValues();
    }

    protected abstract void InitializeHandlers();
    protected abstract float GetShowDuration();
    protected abstract float GetHideDuration();

    private void StoreInitialValues()
    {
        if (_isInitialized) return;
        InitializeHandlers();
        if (startValues == null) 
            SaveStartValues();
        
        if (setStartValues)
        {
            startValues.ApplyTo(RectTransform, _canvasGroup);
        }
        if (disableOnStart)
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
        _isInitialized = true;
    }

    [Button("Save Start Values")]
    private void SaveStartValues()
    {
        startValues = new TempValues();
        startValues.SetInitialState(RectTransform, _canvasGroup);
    }
    
    [Button]
    public void Show()
    {
        if (Application.isPlaying) Show(null);
    }

    public virtual void Show(Action callback)
    {
        StoreInitialValues();
        KillAllSequences();
        
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
        
        ShowStart();
        OnShowStarted?.Invoke();
        
        CurrentSequence = DOTween.Sequence();
        var duration = GetShowDuration();
        
        foreach (var handler in _showHandlers)
        {
            handler.AddToSequence(CurrentSequence, startValues, RectTransform, _canvasGroup, duration);
        }
        
        CurrentSequence.SetUpdate(true);
        CurrentSequence.OnComplete(() =>
        {
            callback?.Invoke();
            ShowEnd();
            OnShowEnded?.Invoke();
        });

        CurrentSequence.Play();
    }

    protected virtual void ShowStart() {}
    protected virtual void ShowEnd() {}

    [Button]
    public void Hide()
    {
        if (Application.isPlaying) Hide(null);
    }

    public virtual void Hide(Action callback)
    {
        StoreInitialValues();
        KillAllSequences();
        
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        
        HideStart();
        OnHideStarted?.Invoke();
        
        CurrentSequence = DOTween.Sequence();
        var duration = GetHideDuration();
        
        foreach (var handler in _hideHandlers)
        {
            handler.AddToSequence(CurrentSequence, startValues, RectTransform, _canvasGroup, duration);
        }
        
        CurrentSequence.SetUpdate(true);
        CurrentSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            callback?.Invoke();
            HideEnd();
            OnHideEnded?.Invoke();
        });

        CurrentSequence.Play();
    }

    protected virtual void HideStart() {}
    protected virtual void HideEnd() {}

    public virtual void InstantHide()
    {
        KillAllSequences();
        
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        
        HideStart();
        OnHideStarted?.Invoke();
        
        gameObject.SetActive(false);
        HideEnd();
        OnHideEnded?.Invoke();
    }
    public virtual void InstantShow()
    {
        StoreInitialValues();
        KillAllSequences();
        
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        _canvasGroup.alpha = 1f;
        
        ShowStart();
        OnShowStarted?.Invoke();
        
        gameObject.SetActive(true);
        ShowEnd();
        OnShowEnded?.Invoke();
    }

    private void KillAllSequences() => CurrentSequence?.Kill();
    private void OnDisable()
    {
        KillAllSequences();
        //startValues.ApplyTo(RectTransform, _canvasGroup);
    }
    
    protected virtual void OnValidate()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}