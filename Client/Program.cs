using System;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Device;

namespace Client
{
    /// <summary>
    /// Консольный Modbus TCP клиент (Master).
    /// 
    /// Этот класс служит точкой входа в приложение клиента. Он отвечает за:
    /// - Подключение к Modbus TCP серверу на 127.0.0.1:502
    /// - Периодическое чтение данных (Coils и Holding Registers)
    /// - Красивое отображение данных в консоли
    /// - Обработку ошибок подключения
    /// 
    /// Основной цикл работы:
    /// 1. Создаётся подключение к серверу 127.0.0.1:502
    /// 2. Инициализируется ModbusClientDataReader для чтения данных
    /// 3. Инициализируется ModbusDataDisplay для красивого вывода
    /// 4. Бесконечный цикл читает данные каждые 500 мс и выводит их
    /// 5. По нажатию клавиши клиент закрывает соединение и выходит
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Modbus TCP Client (Master) ===");
            Console.WriteLine("Подключение к серверу на 127.0.0.1:502...\n");

            // Параметры сервера (IP адрес и порт)
            string serverIp = "127.0.0.1";
            int serverPort = 502;

            try
            {
                // Создаём TCP клиент и Modbus Master
                // TCP клиент отвечает за низкоуровневое соединение
                var tcpClient = new TcpClientRx(serverIp, serverPort);

                // ModbusIpMaster — высокоуровневый интерфейс для работы с Modbus TCP
                using var master = ModbusIpMaster.CreateIp(tcpClient);

                Console.WriteLine("✓ Подключено к серверу.");
                Console.WriteLine("Нажми Ctrl+C для выхода.\n");

                // Инициализируем класс для чтения данных Modbus
                var dataReader = new ModbusClientDataReader(master, slaveAddress: 1);

                // Инициализируем класс для красивого отображения данных
                var display = new ModbusDataDisplay();

                // Основной цикл чтения и отображения данных
                // Не блокирует основной поток благодаря async/await
                while (!Console.KeyAvailable)
                {
                    try
                    {
                        // Читаем все данные (Coils и Registers) с сервера одновременно
                        var (coils, registers) = await dataReader.ReadAllDataAsync();

                        // Проверяем, что данные успешно прочитаны
                        if (coils != null && registers != null)
                        {
                            // Обновляем отображение с типизированными данными
                            // (INT, REAL, STRING, DATE, DWORD)
                            display.UpdateWithTypes(coils, registers);

                            // Выводим положительный статус
                            display.WriteStatus("[✓] Данные обновлены | Waiting...");
                        }
                        else
                        {
                            display.WriteStatus("[!] Не удалось прочитать данные");
                        }
                    }
                    catch (ModbusReadException ex)
                    {
                        // Специфичная ошибка Modbus (проблема с чтением)
                        display.WriteStatus($"[ERROR] Modbus: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // Другие ошибки (проблема с соединением, таймаут и т.д.)
                        display.WriteStatus($"[ERROR] {ex.GetType().Name}: {ex.Message}");
                    }

                    // Небольшая задержка перед следующей итерацией
                    // Не перегружаем сеть и процессор частыми запросами
                    await Task.Delay(500);
                }

                // Пользователь нажал клавишу — завершаем работу
                Console.WriteLine("\n\nЗавершение клиента...");
            }
            catch (Exception ex)
            {
                // Критическая ошибка на этапе инициализации (например, сервер не запущен)
                Console.WriteLine($"[FATAL ERROR] Не удалось подключиться к серверу: {ex.Message}");
                Console.WriteLine("\nУбедитесь, что:");
                Console.WriteLine("1. Сервер запущен (Server.exe)");
                Console.WriteLine("2. Сервер слушает на 127.0.0.1:502");
                Console.WriteLine("3. Порт 502 не заблокирован брандмауэром");
            }
        }
    }
}
