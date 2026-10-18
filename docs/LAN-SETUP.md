# LAN Setup

How to run ShikkhaDesk on several computers sharing one database.

Status: **Built** · **not yet tested between two machines** — see `KNOWN-ISSUES.md` O-5.
Everything below describes what the code is written to do. Follow it carefully and report what
actually happens.

---

## 1. The shape of it

```
        ┌──────────────┐        LAN         ┌──────────────┐
        │  Workstation │  ───────────────▶  │     Host     │
        │   (client)   │   HTTP /api/invoke │  (owns the   │
        └──────────────┘                    │   database)  │
        ┌──────────────┐        LAN         │              │
        │  Workstation │  ───────────────▶  │  ShikkhaDesk │
        │   (client)   │                    │    .db       │
        └──────────────┘                    └──────────────┘
```

- **One machine owns the database.** Every other machine asks it to do the work.
- A client holds **no database connection at all**. It cannot corrupt the file, because it never
  opens it.
- Every permission check, validation and ledger posting happens **once, on the host**. A client
  cannot skip a check by talking to the database directly, because it cannot talk to the database.

## 2. Setting up the host

1. On the machine that will hold the data, install ShikkhaDesk and complete the setup wizard.
2. Open **সেটিংস → নেটওয়ার্ক** (or the LAN settings page).
3. Choose **ল্যান হোস্ট — অন্য কম্পিউটার সংযুক্ত হবে**.
4. Leave the address blank to listen on every network interface, or set a specific one.
5. Set the port. **5188** is the default the setup wizard writes; the settings page falls back to
   **8787** if the box is empty — set it explicitly so the two never disagree.
6. Press **সংরক্ষণ**, then **হোস্ট চালু করুন**.

### Give the host a fixed address

This is the single most common cause of "it worked yesterday".

If the host gets its address from DHCP, it can change after a restart, and every client will be
knocking on the wrong door. Either:

- reserve the address for the host's MAC address in the router's DHCP settings, or
- set a static IP on the host's network adapter.

### Allow the port through Windows Firewall

On the host, an inbound rule is needed for the chosen port. Without it, clients see a timeout with
no explanation.

```powershell
New-NetFirewallRule -DisplayName "ShikkhaDesk LAN Host" -Direction Inbound `
  -Protocol TCP -LocalPort 5188 -Action Allow -Profile Private
```

Use `-Profile Private`. Do **not** open the port on a Public network profile.

## 3. Setting up a client

1. Install ShikkhaDesk on the workstation. **Do not run the setup wizard** — there is no second
   database to set up.
2. Open **সেটিংস → নেটওয়ার্ক**.
3. Choose **ল্যান ক্লায়েন্ট — এই কম্পিউটার হোস্টে যুক্ত হবে**.
4. Enter the host's address and port, e.g. `192.168.1.10` and `5188`.
5. Press **সংযোগ পরীক্ষা**.

If it works you will see the institution's name, the host version and the number of connected
clients. If it does not, see section 5.

6. Press **সংরক্ষণ**, then restart the application and sign in with a username that exists on the
   host. Permissions come from the host, so a teacher's account is a teacher's account on every
   machine.

## 4. Running the host as a Windows service

For a host that should be available whenever the machine is on, run `ShikkhaDesk.Lan.Host` as a
service instead of leaving a console window open.

```powershell
sc.exe create ShikkhaDeskLanHost binPath= "C:\Program Files\ShikkhaDesk\lan-host\ShikkhaDesk.Lan.Host.exe" start= auto
sc.exe start ShikkhaDeskLanHost
```

Run it in a console instead while diagnosing:

```powershell
ShikkhaDesk.Lan.Host.exe --console --port 5188
```

The console prints the addresses clients should use, which is exactly what you want when setting
up for the first time.

## 5. When it does not connect

Work through these in order. Most problems are the first one.

| Symptom | Likely cause | What to check |
|---|---|---|
| Connection refused | Host not running | Is the host service started? Try `--console`. |
| Timeout, no error | Firewall | Inbound rule on the host; is the network profile **Private**? |
| Connected yesterday, not today | Host address changed | Give the host a fixed address. |
| Wrong port | Two different defaults | Compare the host's port with the client's. |
| Login rejected | User must exist on the **host** | Create the user on the host. |
| "Session expired" | 8-hour sliding expiry, or the host restarted | Sign in again. |
| Institutional name blank | Host not set up yet | Finish the wizard on the host. |

The client's log is at `%LOCALAPPDATA%\ShikkhaDesk\Logs`. The host writes to
`%ProgramData%\ShikkhaDesk\Logs`.

## 6. Security

**Be blunt about this: traffic between a client and the host is not encrypted.** It is plain HTTP
carrying student names, phone numbers and payment amounts.

Therefore:

- Run the host on a **trusted, access-controlled network**. Not guest Wi-Fi. Not a shared office
  network you do not control.
- Restrict the firewall rule to the Private profile, and ideally to the source addresses of your
  own workstations.
- Do not expose the port to the internet through port forwarding. Ever.

Credentials are not routed through the invoke endpoint: `Login` and `ChangePassword` are refused
remotely by design, so a client handles them against its own local services.

See `docs/SECURITY.md` limitation **L-2**.

## 7. Known gaps

- **Not tested between two real machines yet.** The path is built; it has not been walked.
- **No transport encryption.**
- **Host discovery is manual.** Clients type the address; there is no browse-for-hosts.
- **No automatic failover.** If the host is down, clients cannot work offline and sync later. This
  is a deliberate design limit, not an oversight: two machines writing to the same ledger
  independently cannot be reconciled afterwards.
