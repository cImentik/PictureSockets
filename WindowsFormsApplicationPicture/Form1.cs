using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

using System.IO;
using System.Net;
using System.Net.Sockets;


/* *
 * Реализовать передачу данных средствами набора сокетов поочередно друг другу. 
 * Первый сокет на входе получает картинку и отсылает ее следующему, который в свою очередь следующему и так далее. 
 * Последний сокет получив картинку выводит ее на форму. Количество сокетов не менее 4.
 * Данные должны передавать непрерывно и не вызывать зависания формы.
 * */
namespace WindowsFormsApplicationPicture
{
    public partial class Form1 : Form
    {
        const int threadCount = 4; //Количество потоков с сокетами
             
        private String g_host = "127.0.0.1"; //начальный хост (он же единственный)
        private int g_port = 50000; //начальный порт (далее увеличиваем на 1 для каждого сокета)
        private FileStream fs; //файловый поток для загружаемого файла
        private Image img; // объект для картинки


        //массив с необходимым количеством воркеров в очереди
        private BackgroundWorker[] backgroundWorkerSockets = new BackgroundWorker[threadCount];


        public Form1()
        {
            InitializeComponent();

            InitializeBackgroundWorker(); 
        }

        /// <summary>
        /// Инициализация воркеров. 
        /// Отделный воркер для загрузки файла и перебор с настройкой массива "рабочих" воркеров
        /// </summary>
        private void InitializeBackgroundWorker()
        {
            backgroundLoadFileWorker.DoWork += new DoWorkEventHandler(
                backgroundLoadFileWorker_DoWork);
            backgroundLoadFileWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                backgroundLoadFileWorker_RunWorkerCompleted);

            for (int i = 0; i < backgroundWorkerSockets.Length; i++)
            {
                backgroundWorkerSockets[i] = new BackgroundWorker();
                backgroundWorkerSockets[i].DoWork += new DoWorkEventHandler(
                    this.DoWork);
                backgroundWorkerSockets[i].RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    this.RunWorkerCompleted);
                backgroundWorkerSockets[i].RunWorkerAsync(i + 1); //сразу слушают порты и ждут файла
            }
        }

        /// <summary>
        /// Один общий DoWork для всех "рабочих" воркеров. В качестве параметра ждёт номер потока
        /// По номеру будет создан нужный порт.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            int threadSocketNumber = (int)e.Argument;
            this.WorkerSocket(threadSocketNumber);
            e.Result = threadSocketNumber; //транслируем дальше в Completed
        }

        /// <summary>
        /// Один общий RunWorkerCompleted для всех "рабочих" воркеров.В качестве параметра ждёт номер потока.
        /// По номеру потоку будет информировать (на форме) об успехе каждого сокета или о неудачи.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int numberOfSocket = (int)e.Result;
            if (e.Error == null)
            {
                Controls["label" + numberOfSocket.ToString()].Text = "Сокет" + numberOfSocket + " - готово";
                if (numberOfSocket == threadCount) //Если это последний сокет из очереди
                    pictureBox1.Image = img;     //то выводим на форму
            }
            else
            {
                this.ShowErrorMessage(numberOfSocket);
            }
        }


        /// <summary>
        /// Обработчик кнопки, который стартует поток на загрузку файла
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    this.backgroundLoadFileWorker.RunWorkerAsync();
                    this.downloadButton.Enabled = false;

                    while (this.backgroundLoadFileWorker.IsBusy)//поток жив 
                    {
                        Application.DoEvents(); 
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Operation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
            
        }


        /// <summary>
        /// DoWork непосредственно выполняющий считывание указанного в диалоге открытия файл.
        /// Также он отправляет файл в первый по очереди сокет
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundLoadFileWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try {
                fs = new FileStream(openFileDialog1.FileName, FileMode.Open);
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    fs.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    MyListener list = new MyListener();
                    list.Sender(memoryStream, g_host, g_port);
                }
                //Thread.Sleep(1000); //чтобы успеть посмотреть
                //fs.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "File operation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
            finally
            {
                fs.Close();
            }
        }


        /// <summary>
        /// Если результат плачевный уведомляем об этом пользователя
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundLoadFileWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error == null)
            {
               //MessageBox.Show("Download Complete");
            }
            else
            {
                MessageBox.Show(
                    "Failed to download file",
                    "Download failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            this.downloadButton.Enabled = true;
        }

        private void ShowErrorMessage(int namberOfSocked)
        {
            MessageBox.Show("Failed to socket" + namberOfSocked.ToString(), "Connected failed",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        }

        /// <summary>
        /// Метод работы с сокетами.
        /// В каждом потоке созаётся экземпляр MyListener, который осуществляет приём и отправку полученного файла
        /// </summary>
        /// <param name="namberOfSocked"></param>
        private void WorkerSocket(int namberOfSocked)
        {
            MyListener list = new MyListener();
            list.Receiver(g_host, g_port + (namberOfSocked - 1)); //Слушаем и получаем файл если он есть
            if (namberOfSocked < threadCount) //если не последний сокет
            {
                list.GetMemorystream().Position = 0;
                //оправить полученый файл в следующий сокет
                list.Sender(list.GetMemorystream(), g_host, g_port + namberOfSocked);  
            }
            else
            { //если это последний сокет ждём и показываем на форме
                Thread.Sleep(2000);
                img = System.Drawing.Image.FromStream(list.GetMemorystream());
            }
        }
    }

    /// <summary>
    /// Вспомогательный класс для логики работы сокета. Обладает в себе возможностью отправки и приёма
    /// файла.
    /// </summary>
    public class MyListener
    {
        private MemoryStream memorystream; //поток для хранения файла между отпракой и посылкой

        public MyListener()
        {
            memorystream = new MemoryStream();
        }

        public MemoryStream GetMemorystream()
        {
            return memorystream;
        }

        /// <summary>
        /// Создание соеденения и отправка файла. Создаётся буфер и файл отправляетя по кускам, читаемым из потока,
        /// эквивалентным размеру буфера
        /// </summary>
        public void Sender(MemoryStream memorystream, String host, int port)
        {
            try
            {
                IPEndPoint EndPoint = new IPEndPoint(IPAddress.Parse(host), port);
                Socket Connector = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                Connector.Connect(EndPoint);
                Byte[] ReadedBytes = new Byte[256];

                using (var Reader = new BinaryReader(memorystream, Encoding.UTF8, true))
                {
                    Int32 CurrentReadedBytesCount;
                    do
                    {
                        CurrentReadedBytesCount = Reader.Read(ReadedBytes, 0, ReadedBytes.Length);
                        Connector.Send(ReadedBytes, CurrentReadedBytesCount, SocketFlags.None);
                    }
                    while (CurrentReadedBytesCount == ReadedBytes.Length);
                }
                Connector.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connected failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
            
        }

        /// <summary>
        /// Ожтдает отправки файла. Если файл получен уложить его в this.memorystream,
        /// чтобы потом утправить методом Sender
        /// </summary>
        public void Receiver(String host, int port)
        {
            try
            {
                MemoryStream mStream = new MemoryStream();
                TcpListener Listen = new TcpListener(IPAddress.Parse(host), port);
                Listen.Start();
                Socket ReceiveSocket;
                while (true)
                {
                    ReceiveSocket = Listen.AcceptSocket();
                    Byte[] Receive = new Byte[256];
                    using (MemoryStream MessageFile = new MemoryStream())
                    {
                        //Количество считанных байт
                        Int32 ReceivedBytes;
                        do
                        {
                            ReceivedBytes = ReceiveSocket.Receive(Receive, Receive.Length, 0);
                            MessageFile.Write(Receive, 0, ReceivedBytes);
                            //Читаем до тех пор, пока в очереди не останется данных
                        } while (ReceivedBytes == Receive.Length);

                        MessageFile.Position = 0;
                        MessageFile.CopyTo(this.memorystream);

                    }
                    break; //не принципиально, но тогда можно явно закончить поток
                    // и отобразить на форме
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connected failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }

        }

    }
}
