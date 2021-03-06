﻿using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Foundation.UI.Common
{
    public class UIButtonAsync
    {
        public async static Task<T> SelectButton<T>(Button[] buttons) where T : Component
        {
            var tasks = buttons.Select(PressButton);
            Task<Button> finish = await Task.WhenAny(tasks);

            return finish.Result.GetComponent<T>();
        }

        public async static Task<Button> PressButton(Button button)
        {
            bool isPressed = false;
            button.onClick.AddListener(() => isPressed = true);
            while (!isPressed)
            {
                if (ReferenceEquals(null, button))
                {
                    Log.Warn("UIButtonAsync null button");
                    return null;
                }
                await Task.Yield();
            }

            return button;
        }
    }
}
