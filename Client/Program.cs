using CP.IO.Ports;
using ModbusRx.Device;

namespace Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Параметры сервера (IP и порт)
            string serverIp = "127.0.0.1";
            int serverPort = 502;

            // Создаём TCP Client и Modbus Master
            var tcpClient = new TcpClientRx(serverIp, serverPort);
            using var master = ModbusIpMaster.CreateIp(tcpClient);

            Console.WriteLine("Modbus TCP Client запущен...");
            Console.WriteLine("Нажми Ctrl+C для выхода.");

            while (true)
            {
                try
                {
                    // 1) Чтение 10 Coil (булевых)
                    bool[] coils = await master.ReadCoilsAsync(
                        slaveAddress: 1, startAddress: 1, numberOfPoints: 10);

                    // 2) Чтение 10 Holding Registers
                    ushort[] holdingRegs = await master.ReadHoldingRegistersAsync(
                        slaveAddress: 1, startAddress: 1, numberOfPoints: 10);

                    Console.WriteLine($"Client Read: Coils 0..9 = {string.Join(",", coils)}");
                    Console.WriteLine($"Client Read: HoldingRegs 0..9 = {string.Join(",", holdingRegs)}");

                    // 3) Запись булевых Coil — инвертировать каждое
                    bool[] coilsToWrite = new bool[10];
                    for (int i = 1; i < coils.Length; i++)
                        coilsToWrite[i] = !coils[i];

                    await master.WriteMultipleCoilsAsync(
                        slaveAddress: 1, startAddress: 1, data: coilsToWrite);

                    // 4) Запись Holding Registers — +10 к каждому
                    ushort[] regsToWrite = new ushort[10];
                    for (int i = 1; i < holdingRegs.Length; i++)
                        regsToWrite[i] = (ushort)(holdingRegs[i] + 10);

                    await master.WriteMultipleRegistersAsync(
                        slaveAddress: 1, startAddress: 1, data: regsToWrite);

                    Console.WriteLine("Client: записи выполнены.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка Modbus: {ex.Message}");
                }

                // Задержка 1 сек
                await Task.Delay(1000);
            }

        }
    }
}
