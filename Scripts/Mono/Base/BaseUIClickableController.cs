using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public abstract class BaseUIClickableController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] protected CanvasGroup _canvasGroup;
    [SerializeField] protected Button _button;
    
    public event Action OnHoverStarted;
    public event Action OnHoverEnded;
    public event Action OnClickStarted;
    public event Action OnClickEnded;
    public event Action OnInteractableChanged;
    
    public RectTransform RectTransform => transform as RectTransform;
    
    protected List<IAnimationHandler> _hoverHandlers;
    protected List<IAnimationHandler> _clickHandlers;
    protected List<IAnimationHandler> _disableHandlers;
    protected List<IAnimationHandler> _returnFromHoverHandlers;
    protected List<IAnimationHandler> _returnFromClickHandlers;
    protected List<IAnimationHandler> _returnFromDisableHandlers;
    
    protected bool _isHovered;
    protected bool _isClicked;
    protected bool _wasInteractable;
    
    protected Sequence CurrentSequence { get; set; }
    protected virtual void Awake()
    {
        InitializeHandlers();
        StoreInitialValues();
        _wasInteractable = _canvasGroup.interactable;
    }
    
    protected virtual void Update()
    {
        if (_canvasGroup.interactable != _wasInteractable)
        {
            _wasInteractable = _canvasGroup.interactable;
            OnInteractableStateChanged();
        }
    }
    
    protected abstract void InitializeHandlers();
    protected abstract float GetHoverDuration();
    protected abstract float GetClickDuration();
    protected abstract float GetDisableDuration();
    protected abstract float GetReturnFromHoverDuration();
    protected abstract float GetReturnFromClickDuration();
    protected abstract float GetReturnFromDisableDuration();

    [SerializeReference]
    private TempValues startValues = null;
    private void StoreInitialValues()
    {
        if (startValues != null) return;
        SaveStartValues();
    }

    [Button("Save Start Values")]
    private void SaveStartValues()
    {
        startValues = new TempValues();
        startValues.SetInitialState(RectTransform, _canvasGroup);
    }
    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (!_canvasGroup.interactable) return;
        
        _isHovered = true;
        PlayAnimation(_hoverHandlers, GetHoverDuration(), OnHoverStarted);
    }
    
    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (!_canvasGroup.interactable) return;
        
        _isHovered = false;
        if (!_isClicked)
        {
            PlayAnimation(_returnFromHoverHandlers, GetReturnFromHoverDuration(), OnHoverEnded);
        }
    }
    
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (!_canvasGroup.interactable) return;
        
        _isClicked = true;
        PlayAnimation(_clickHandlers, GetClickDuration(), OnClickStarted);
    }
    
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        if (!_canvasGroup.interactable) return;
        
        _isClicked = false;
        var returnHandlers = _isHovered ? _returnFromClickHandlers : _returnFromHoverHandlers;
        var returnDuration = _isHovered ? GetReturnFromClickDuration() : GetReturnFromHoverDuration();
        PlayAnimation(returnHandlers, returnDuration, OnClickEnded);
    }
    
    protected virtual void OnInteractableStateChanged()
    {
        OnInteractableChanged?.Invoke();
        
        if (!_canvasGroup.interactable)
        {
            _isHovered = false;
            _isClicked = false;
            PlayAnimation(_disableHandlers, GetDisableDuration(), null);
        }
        else
        {
            PlayAnimation(_returnFromDisableHandlers, GetReturnFromDisableDuration(), null);
        }
    }
    
    protected virtual void PlayAnimation(List<IAnimationHandler> handlers, float duration, Action callback)
    {
        if (handlers == null || handlers.Count == 0) return;
        StoreInitialValues();
        KillAllSequences();
        
        CurrentSequence = DOTween.Sequence();
        
        foreach (var handler in handlers)
        {
            handler?.AddToSequence(CurrentSequence, startValues, RectTransform, _canvasGroup, duration);
        }

        CurrentSequence.SetUpdate(true);
        CurrentSequence.OnComplete(() => callback?.Invoke());
        CurrentSequence.Play();
    }
    
    protected void KillAllSequences() => CurrentSequence?.Kill();

    private void OnDisable()
    {
        KillAllSequences();
        startValues.ApplyTo(RectTransform, _canvasGroup);
    }
}