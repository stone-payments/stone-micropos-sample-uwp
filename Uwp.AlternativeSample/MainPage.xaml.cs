using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CrossPlatform.Uwp.AlternativeSample
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Pizza machine.
        /// </summary>
        private IPizzaMachine pizzaMachine;
        /// <summary>
        /// Pizza picked ID. Get by the click of a button.
        /// </summary>
        public string PizzaPickedId;

        /// <summary>
        /// Creates all screen components.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            this.pizzaMachine = new PizzaMachine();
            this.ChangePizzaButtonState(false);
            Loaded += this.OnBegin;
        }

        /// <summary>
        /// Called when the screen is opened. Triggers an event that is executed only when all components are created.
        /// </summary>
        /// <param name="sender">Window.</param>
        /// <param name="e">Load event arguments.</param>
        private void OnBegin(object sender, RoutedEventArgs e)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(this.OnActuallyBegin)).AsTask();
        }
        /// <summary>
        /// Executed when the window is completed loaded.
        /// Turns on the pizza machine.
        /// </summary>
        private void OnActuallyBegin()
        {
            this.PizzaPickedId = null;
            this.pizzaMachine.View = this;
            Task.Run(() => this.pizzaMachine.TurnOn());
        }
        /// <summary>
        /// Called when a pizza is picked.
        /// </summary>
        /// <param name="sender">Pizza button.</param>
        /// <param name="e">Click event arguments.</param>
        private void OnPizzaPicked(object sender, RoutedEventArgs e)
        {
            this.PizzaPickedId = (sender as Button).Content.ToString();
        }
        /// <summary>
        /// Called when pizza button are onabled or disabled.
        /// Used to control whether the user can pick the pizza or not.
        /// </summary>
        /// <param name="state"></param>
        public void ChangePizzaButtonState(bool state)
        {
            IEnumerable<Button> buttons = this.uxGridButtons.Children.OfType<Button>();

            foreach (Button b in buttons) { b.IsEnabled = state; }

            Dispatcher.RunAsync(CoreDispatcherPriority.Low, new DispatchedHandler(delegate { })).AsTask();
        }
        /// <summary>
        /// Turns off the pizza machine. In this case, when the window is closed.
        /// </summary>
        /// <param name="sender">Window.</param>
        /// <param name="e">Close event arguments.</param>
        public void OnClosePizzeria(object sender, RoutedEventArgs e)
        {
            this.pizzaMachine.TurnOff();
        }
    }
}
