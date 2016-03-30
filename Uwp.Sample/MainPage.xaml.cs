using MicroPos.Core;
using MicroPos.Core.Authorization;
using Pinpad.Sdk.Connection;
using Pinpad.Sdk.Model.Exceptions;
using Pinpad.Sdk.Model.TypeCode;
using Poi.Sdk;
using Poi.Sdk.Authorization;
using Poi.Sdk.Cancellation;
using Poi.Sdk.Model._2._0;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CrossPlatformUniversalApp.Sample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Constructor
        public MainPage()
        {
            this.InitializeComponent();
        }

        // Methods
        /// <summary>
        /// Create all instances needed to perform MicroPos operations, called on form loading.
        /// </summary>
        /// <param name="sender">Form loading parameters.</param>
        /// <param name="e">Loading event arguments.</param>
        private void Setup(object sender, RoutedEventArgs e)
        {
            // Inicializa a plataforma desktop:
            CrossPlatformUniversalApp.CrossPlatformUniversalAppInitializer.Initialize();

            // Constrói as mensagens que serão apresentadas na tela do pinpad:
            DisplayableMessages pinpadMessages = new DisplayableMessages();
            pinpadMessages.ApprovedMessage = ":-)";
            pinpadMessages.DeclinedMessage = ":-(";
            pinpadMessages.InitializationMessage = "Ola";
            pinpadMessages.MainLabel = "Stone Pagamentos";
            pinpadMessages.ProcessingMessage = "Processando...";

            this.approvedTransactions = new Collection<TransactionModel>();

            // Inicializa o autorizador
            this.authorizer = new CardPaymentAuthorizer(this.sak, this.authorizationUri, this.tmsUri, null, pinpadMessages);
            Debug.WriteLine("Command OPN");
            this.authorizer.OnStateChanged += this.OnTransactionStateChange;

            this.uxBtnCancelTransaction.IsEnabled = false;
        }
        /// <summary>
        /// Perform an authorization process.
        /// </summary>
        /// <param name="sender">Send transaction button.</param>
        /// <param name="e">Click event arguments.</param>
        private void InitiateTransaction(object sender, RoutedEventArgs e)
        {
            // Limpa o log:
            this.uxLvwLog.Items.Clear();

            // Cria uma transação:
            // Tipo da transação inválido significa que o pinpad vai perguntar ao usuário o tipo da transação.
            TransactionType transactionType;
            Installment installment = new Installment();
            if (this.uxCbxItemDebit.IsSelected == true)
            {
                transactionType = TransactionType.Debit;

                // É débito, então não possui parcelamento:
                installment.Number = 1;
                installment.Type = InstallmentType.None;
            }
            else if (this.uxCbxItemCredit.IsSelected == true)
            {
                transactionType = TransactionType.Credit;

                // Cria o parcelamento:
                installment.Number = Int16.Parse(this.uxTbxInstallmentNumber.Text);
                installment.Type = (this.uxTggInstallmentType.IsOn == true) ? InstallmentType.Issuer : InstallmentType.Merchant;
            }
            else
            {
                transactionType = TransactionType.Undefined;
            }

            // Pega o valor da transação
            decimal amount;
            decimal.TryParse(this.uxTbxAmount.Text, out amount);
            if (amount == 0)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Valor da transaçào inválido."); }).AsTask();
                return;
            }

            // Cria e configura a transação:
            TransactionEntry transaction = new TransactionEntry(transactionType, amount);
            transaction.Installment = installment;
            transaction.InitiatorTransactionKey = this.uxTbxTransactionId.Text;
            transaction.CaptureTransaction = true;
            ICard card;

            // Envia para o autorizador:
            PoiResponseBase poiResponse = null;

            try
            {
                poiResponse = this.authorizer.Authorize(transaction, out card);
            }
            catch (ExpiredCardException)
            {
                this.Log("Card Expired");
                this.authorizer.PromptForCardRemoval("CARTAO EXPIRADO");
                return;
            }

            if (poiResponse == null)
            {
                this.Log("Um erro ocorreu durante a transação.");
                return;
            }

            if (poiResponse == null)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Um erro ocorreu durante a transação."); }).AsTask();
                return;
            }

            // Verifica o retorno do autorizador:
            if (poiResponse.Rejected == false && this.WasDeclined(poiResponse.OriginalResponse as AcceptorAuthorisationResponse) == false)
            {
                // Transaction approved:
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Transação aprovada."); }).AsTask();

                // Cria uma instancia de transaçào aprovada:
                TransactionModel approvedTransaction = TransactionModel.Create(transaction, card, poiResponse as AuthorizationResponse);

                // Salva em uma collection:
                this.approvedTransactions.Add(approvedTransaction);

                // Adiciona o ATK (identificador unico da transação) ao log:
                this.uxLvwTransactionList.Items.Add(approvedTransaction.AuthorizationTransactionKey);
            }
            else if (poiResponse.Rejected == false && this.WasDeclined(poiResponse.OriginalResponse as AcceptorAuthorisationResponse) == true)
            {
                // Transaction declined:
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Transação declinada."); }).AsTask();
            }
            else if (poiResponse.Rejected == true && poiResponse is Rejection)
            {
                // Transaction rejected:
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Transação rejeitada."); }).AsTask();
            }
        }
        /// <summary>
        /// Called when the transaction status has changed.
        /// It log the current transaction status.
        /// </summary>
        /// <param name="sender">Authorization process.</param>
        /// <param name="e">Authorization status changing event arguments.</param>
        private void OnTransactionStateChange(object sender, AuthorizationStatusChangeEventArgs e)
        {
            this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log(e.AuthorizationStatus + " " + e.Message); }).AsTask();
        }
        /// <summary>
        /// Transaction handler. If a transaction is selected, then enables cancellation button.
        /// </summary>
        /// <param name="sender">Transaction list.</param>
        /// <param name="e">Selection event arguments.</param>
        private void OnTransactionSelected(object sender, RoutedEventArgs e)
        {
            if (this.uxLvwTransactionList.SelectedItems.Count > 1 || this.uxLvwTransactionList.SelectedItems.Count <= 0)
            {
                this.uxBtnCancelTransaction.IsEnabled = false;
            }
            else
            {
                this.uxBtnCancelTransaction.IsEnabled = true;
            }
        }
        /// <summary>
        /// Updates pinpad screen with input labels.
        /// </summary>
        /// <param name="sender">Screen update button.</param>
        /// <param name="e">Click event arguments.</param>
        private void ShowPinpadLabel(object sender, RoutedEventArgs e)
        {
            DisplayPaddingType pinpadAlignment;
            if (this.uxCbxItemRight.IsSelected == true)
            {
                pinpadAlignment = DisplayPaddingType.Right;
            }
            else if (this.uxCbxItemCenter.IsSelected == true)
            {
                pinpadAlignment = DisplayPaddingType.Center;
            }
            else
            {
                pinpadAlignment = DisplayPaddingType.Left;
            }

            if (this.authorizer.PinpadController.Display.ShowMessage(this.uxTbxFirstRow.Text, this.uxTbxSecondRow.Text, pinpadAlignment) == true)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Mensagem mostrada na tela do pinpad."); }).AsTask();
            }
            else
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("A mensagem não foi mostrada."); }).AsTask();
                
            }

            if (this.uxChxWaitKey.IsChecked == true)
            {
                PinpadKeyCode key = PinpadKeyCode.Undefined;
                do { key = this.authorizer.PinpadController.Keyboard.GetKey(); }
                while (key == PinpadKeyCode.Undefined);

            }
        }
        /// <summary>
        /// Performs a cancellation operation.
        /// </summary>
        /// <param name="sender">Cancellation button.</param>
        /// <param name="e">Click event arguments.</param>
        private void CancelTransaction(object sender, RoutedEventArgs e)
        {
            string atk = this.uxLvwTransactionList.SelectedItem.ToString();

            // Verifica se um ATK válido foi selecionado:
            if (string.IsNullOrEmpty(atk) == true)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Não é possivel cancelar um ATK vazio."); }).AsTask();
                return;
            }

            // Seleciona a transação a ser cancelada de acordo com o ATK:
            TransactionModel transaction = this.approvedTransactions.Where(t => t.AuthorizationTransactionKey == atk).First();

            // Cria a requisiçào de cancelamento:
            CancellationRequest request = CancellationRequest.CreateCancellationRequestByAcquirerTransactionKey(this.sak, atk, transaction.Amount, true);

            // Envia o cancelamento:
            PoiResponseBase response = this.authorizer.AuthorizationProvider.SendRequest(request);

            if (response is Rejection || this.WasDeclined(response.OriginalResponse as AcceptorCancellationResponse) == true)
            {
                // Cancelamento não autorizado:
                this.Log(this.GetDeclinedMessage(response.OriginalResponse as AcceptorCancellationResponse));
            }
            else
            {
                // Cancelamento autorizado.
                // Retira a transação da coleção de transação aprovadas:
                this.approvedTransactions.Remove(transaction);
                this.uxLvwTransactionList.Items.Remove(transaction.AuthorizationTransactionKey);
            }
        }
        /// <summary>
        /// Verifies if the pinpad is connected or not.
        /// </summary>
        /// <param name="sender">Ping button.</param>
        /// <param name="e">Click event arguments.</param>
        private void PingPinpad(object sender, RoutedEventArgs e)
        {
            if (this.authorizer.PinpadController.PinpadConnection.Ping() == true)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("O pinpad está conectado."); }).AsTask();
            }
            else
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("O pinpad está DESCONECTADO."); }).AsTask();
            }
        }
        /// <summary>
        /// Try pinpad reconnection.
        /// </summary>
        /// <param name="sender">Reconnection button.</param>
        /// <param name="e">Click event arguments.</param>
        private void Reconnect(object sender, RoutedEventArgs e)
        {
            // Procura a porta serial que tenha um pinpad conectado e tenta estabelecer conexão com ela:
            this.authorizer.PinpadController.PinpadConnection.Open(PinpadConnection.SearchPinpadPort());

            // Verifica se conseguiu se conectar:
            if (this.authorizer.PinpadController.PinpadConnection.IsOpen == true)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Pinpad conectado."); }).AsTask();
            }
            else
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Pinpad desconectado."); }).AsTask();
            }
        }
        /// <summary>
        /// Get secure PAN.
        /// </summary>
        /// <param name="sender">Get PAN button.</param>
        /// <param name="e">Click event arguments</param>
        private void GetPan(object sender, RoutedEventArgs e)
        {
            string maskedPan;

            // Get PAN:
            AuthorizationStatus status = this.authorizer.GetSecurePan(out maskedPan);

            // Verifies if PAN was captured correctly:
            if (string.IsNullOrEmpty(maskedPan) == true || status != AuthorizationStatus.Approved)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("O PAN não pode ser capturado."); }).AsTask();
            }
            else
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log(string.Format("PAN capturado: {0}", maskedPan)); }).AsTask();
            }
        }
        /// <summary>
        /// Performs a forced download of pinpad tables.
        /// </summary>
        /// <param name="sender">Download tables button.</param>
        /// <param name="e">Click event arguments.</param>
        private void DownloadTables(object sender, RoutedEventArgs e)
        {
            bool success = this.authorizer.UpdateTables(1, false);
            if (success == true)
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Tabelas atualizadas com sucesso."); }).AsTask();
            }
            else
            {
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.Log("Erro ao atualizar as tabelas."); }).AsTask();
            }
        }
    }
}
