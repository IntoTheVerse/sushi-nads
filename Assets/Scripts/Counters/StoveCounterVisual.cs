using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class StoveCounterVisual : MonoBehaviour
{
    public enum Cookware
    {
        RiceCooker,
        Saucepan,
        Pan,
        Grinder
    }

    public Cookware cookware;
    [SerializeField] private TimerPulse timerPulse; // drag the TimerPulse component here
    [SerializeField] private StoveCounter stoveCounter;
    [SerializeField] private GameObject stoveOnGameObject;
    [SerializeField] private GameObject particlesGameObject;
    [SerializeField] private GameObject buttonOff;
    [SerializeField] private GameObject CookerLid;
    [SerializeField] ParticleSystem ps;
    [SerializeField] float interval = 1f;
    [SerializeField] int burstCount = 300;
    [SerializeField] private Animator CookerLidAnimator;
    [SerializeField] private Transform timerIconNeedle;   // drag Image.rectTransform or a GameObject here
    [SerializeField] private Transform timerIconNeedleBackground;   // drag Image.rectTransform or a GameObject here
    [SerializeField] private Transform timerIcon;   // drag Image.rectTransform or a GameObject here
    [SerializeField] private bool useRadialFill = true;
    [SerializeField] private bool invertRadial = false; // tick if you want a countdown look
    private Image _radialImg; // cached Image on timerIconNeedle
    [SerializeField] private Sprite _timerNeedleBurnedSprite;
    [SerializeField] private Sprite _timerNeedleBackgroundBurnedSprite;
    [SerializeField] private Sprite _timerNeedleNormalSprite;
    [SerializeField] private Sprite _timerNeedleBackgroundNormalSprite;
    [SerializeField] private float degreesPerSecond = 180f;  // 360f / secondsPerTurn
    [SerializeField] private bool useUnscaledTime = false;   // true if you want it to spin during pause

    private static readonly int OpenHash = Animator.StringToHash("Open");
    private static readonly int OpenCloseHash = Animator.StringToHash("OpenClose");
    Coroutine loop;
    private void Start() {
        stoveCounter.OnStateChanged += StoveCounter_OnStateChanged;
        stoveCounter.OnProgressChanged += StoveCounter_OnProgressChanged; // NEW
        _radialImg = timerIconNeedleBackground ? timerIconNeedleBackground.GetComponent<Image>() : null;
        if (useRadialFill && _radialImg != null)
        {
            _radialImg.type = Image.Type.Filled;
            _radialImg.fillMethod = Image.FillMethod.Radial360;
            _radialImg.fillOrigin = (int)Image.Origin360.Top; // pick Top/Right/Bottom/Left to taste
            _radialImg.fillClockwise = true;
            _radialImg.fillAmount = invertRadial ? 1f : 0f;   // reset
        }
    }
    void OnEnable()
    {
        BaseCounter.OnAnyObjectPlacedHere += HandleAnyPlaced;
        BaseCounter.OnAnyObjectRemovedHere += HandleAnyRemoved;

        if (!ps) ps = GetComponent<ParticleSystem>();
        if (loop == null) loop = StartCoroutine(EmitLoop());
    }

    private void StoveCounter_OnProgressChanged(object sender, IHasProgress.OnProgressChangedEventArgs e)
    {
        if (cookware == Cookware.Saucepan && timerPulse)
        {
            // e.progressNormalized: 0 -> 1 while frying
            timerPulse.UpdateUrgency(e.progressNormalized);
        }
        else if (cookware == Cookware.Grinder && timerPulse)
        {
            // e.progressNormalized: 0 -> 1 while frying
            timerPulse.UpdateUrgency(e.progressNormalized);
        }
        else if (cookware == Cookware.RiceCooker && timerPulse)
        {
            // e.progressNormalized: 0 -> 1 while frying
            timerPulse.UpdateUrgency(e.progressNormalized);
        }
        if (useRadialFill && _radialImg)
        {
            float v = Mathf.Clamp01(e.progressNormalized);
            _radialImg.fillAmount = invertRadial ? (1f - v) : v;
        }
    }

    IEnumerator EmitLoop()
    {
        float onDuration = 0.4f;   // how long the hiss lasts
        float offDuration = 0.6f;  // gap so total = 1s
        float ratePerSecond = 200f;

        var emission = ps.emission;

        while (true)
        {
            // ON: continuous spawn
            emission.rateOverTime = ratePerSecond;
            ps.Play();
            yield return new WaitForSeconds(onDuration);

            // OFF: stop spawning
            emission.rateOverTime = 0f;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); // keeps existing particles alive
            yield return new WaitForSeconds(offDuration);
        }
    }
    private void HandleAnyPlaced(object sender, EventArgs e)
    {
        /*if (sender == stoveCounter)
        {
            SetCookUIActive(true);   // enable steam/timer when item dropped here
        }*/
    }

    private void HandleAnyRemoved(object sender, EventArgs e)
    {
        if (sender != stoveCounter) return;
        if (cookware != Cookware.Saucepan &&
            cookware != Cookware.RiceCooker &&
            cookware != Cookware.Grinder) return;
        StartCoroutine(AfterRemovalCheck_Saucepan());
    }
    private IEnumerator AfterRemovalCheck_Saucepan()
    {
        // allow the fried item to be spawned this frame (swap)
        yield return null;

        // Only kill UI if the player actually took the item
        if (!stoveCounter.HasKitchenObject() && stoveCounter.GetState() == StoveCounter.State.Idle)
        {
            SetCookUIActive(false);    // turns off saucepan PS & timer
        }
    }
    private void SetCookUIActive(bool active)
    {
        if (timerIcon) timerIcon.gameObject.SetActive(active);
        if (ps) ps.gameObject.SetActive(active);
    }

    private void StoveCounter_OnStateChanged(object sender, StoveCounter.OnStateChangedEventArgs e) {
        Debug.Log($"state {e.state}");
        bool showVisual = e.state == StoveCounter.State.Frying || e.state == StoveCounter.State.Fried;
        if(stoveOnGameObject)
            stoveOnGameObject.SetActive(showVisual);
        if(particlesGameObject)
            particlesGameObject.SetActive(showVisual);
        if(buttonOff)
            buttonOff.SetActive(!showVisual);
        if (CookerLid)
        {
            //CookerLid x = -45 means cooker lid is open 
            //x = 0 means cooker lid is closed 
            //The lid will be open in idle position until player puts something to cook then it closes then it will open automatically when food is cooked
            //showVisual is bool value true means something is getting cooked false means it cooked already
            const float openX = -45f;
            const float closedX = 0f;
            const float degreesPerSecond = 320f; // opening/closing speed

            float targetX = showVisual ? closedX : openX;
            if (e.state == StoveCounter.State.Frying)
            {
                CookerLidAnimator.SetTrigger(OpenCloseHash);
                if (ps && cookware == Cookware.RiceCooker)
                {
                    ps.gameObject.SetActive(true);
                }
            }
            else if(e.state == StoveCounter.State.Fried)
            {
                CookerLidAnimator.SetTrigger(OpenHash);
                if (ps && cookware == Cookware.RiceCooker)
                {
                    ps.gameObject.SetActive(false);
                }
            }
            // Smoothly move only the X angle to the target
        }
        if (e.state == StoveCounter.State.Frying)
        {
            //if (timerPulse) timerPulse.StartPulse();
            if (ps && cookware == Cookware.Saucepan)
            {
                timerIconNeedle.GetComponent<Image>().sprite = _timerNeedleNormalSprite;
                timerIconNeedleBackground.GetComponent<Image>().sprite = _timerNeedleBackgroundNormalSprite;
                timerIcon.gameObject.SetActive(true);
                ps.gameObject.SetActive(true);
                if (useRadialFill && _radialImg)
                {
                    _radialImg.fillAmount = invertRadial ? 1f : 0f;  // start from empty (or full if inverted)
                }
            }
            else if (cookware == Cookware.RiceCooker)
            {
                timerIconNeedle.GetComponent<Image>().sprite = _timerNeedleNormalSprite;
                timerIconNeedleBackground.GetComponent<Image>().sprite = _timerNeedleBackgroundNormalSprite;
                timerIcon.gameObject.SetActive(true);
                if (useRadialFill && _radialImg)
                {
                    _radialImg.fillAmount = invertRadial ? 1f : 0f;  // start from empty (or full if inverted)
                }
            }
            else if (cookware == Cookware.Grinder)
            {
                timerIconNeedle.GetComponent<Image>().sprite = _timerNeedleNormalSprite;
                timerIconNeedleBackground.GetComponent<Image>().sprite = _timerNeedleBackgroundNormalSprite;
                timerIcon.gameObject.SetActive(true);
                if (useRadialFill && _radialImg)
                {
                    _radialImg.fillAmount = invertRadial ? 1f : 0f;  // start from empty (or full if inverted)
                }
            }
        }
        else if(e.state == StoveCounter.State.Fried)
        {
            if (timerPulse) timerPulse.StartPulse();
            if (ps && cookware == Cookware.Saucepan)
            {
                timerIconNeedle.GetComponent<Image>().sprite = _timerNeedleBurnedSprite;
                timerIconNeedleBackground.GetComponent<Image>().sprite = _timerNeedleBackgroundBurnedSprite;
                //ps.gameObject.SetActive(false);
                if (useRadialFill && _radialImg)
                {
                    _radialImg.fillAmount = invertRadial ? 0f : 1f;  // complete the ring
                }
            }
            else if (cookware == Cookware.RiceCooker)
            {
                timerIconNeedle.GetComponent<Image>().sprite = _timerNeedleBurnedSprite;
                timerIconNeedleBackground.GetComponent<Image>().sprite = _timerNeedleBackgroundBurnedSprite;
                if (useRadialFill && _radialImg)
                {
                    _radialImg.fillAmount = invertRadial ? 0f : 1f;  // complete the ring
                }
            }
            else if (cookware == Cookware.Grinder)
            {
                timerIconNeedle.GetComponent<Image>().sprite = _timerNeedleBurnedSprite;
                timerIconNeedleBackground.GetComponent<Image>().sprite = _timerNeedleBackgroundBurnedSprite;
                if (useRadialFill && _radialImg)
                {
                    _radialImg.fillAmount = invertRadial ? 0f : 1f;  // complete the ring
                }
            }
        }
        else if (e.state == StoveCounter.State.Burned)
        {
            if (timerPulse) timerPulse.StopPulse();
            timerIcon.gameObject.SetActive(false);
            //if (timerPulse) timerPulse.StopPulse();
            if (ps && cookware == Cookware.Saucepan)
            {

                //ps.gameObject.SetActive(false);
            }
        }
    }
    private void Update()
    {
        
    }

    void OnDisable()
    {
        BaseCounter.OnAnyObjectPlacedHere -= HandleAnyPlaced;
        BaseCounter.OnAnyObjectRemovedHere -= HandleAnyRemoved;
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

}
