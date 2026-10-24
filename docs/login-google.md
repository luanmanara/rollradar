# Login com Google (teste local)

Passo a passo para testar o login do RollRadar na sua máquina. Sem estas chaves a API sobe normalmente, só o
botão "Entrar com Google" responde 503.

## 1. Criar o OAuth client no Google Cloud

1. Abra <https://console.cloud.google.com/> e crie um projeto (ex.: `RollRadar`) ou escolha um existente.
2. Menu **APIs e serviços → Tela de consentimento OAuth** (ou **Google Auth Platform → Branding**):
   - Tipo de usuário: **Externo**.
   - Nome do app: `RollRadar`; e-mail de suporte e de contato: o seu.
   - Escopos: não precisa adicionar nada além dos padrão (`openid`, `email`, `profile`).
   - Em **Público-alvo / Usuários de teste**, adicione o seu e-mail do Google. Enquanto o app estiver em
     "Teste", só esses e-mails conseguem entrar.
3. Menu **APIs e serviços → Credenciais → Criar credenciais → ID do cliente OAuth**:
   - Tipo de aplicativo: **Aplicativo da Web**.
   - Nome: `RollRadar local`.
   - **URIs de redirecionamento autorizados** (os dois):
     - `http://localhost:4200/api/auth/google-callback` (site pelo `npm start`, o caminho normal)
     - `http://localhost:5214/api/auth/google-callback` (API direto, ex.: pelo Swagger)
   - Não precisa preencher "Origens JavaScript autorizadas".
4. Clique em **Criar** e copie o **ID do cliente** e a **Chave secreta do cliente**.

## 2. Guardar as chaves nos user-secrets da API

No PowerShell, na pasta do projeto (as chaves ficam fora do repositório, nunca no `appsettings.json`):

```powershell
dotnet user-secrets set "Auth:Google:ClientId" "SEU-ID.apps.googleusercontent.com" --project backend/RollRadar.Api
dotnet user-secrets set "Auth:Google:ClientSecret" "SUA-CHAVE-SECRETA" --project backend/RollRadar.Api
dotnet user-secrets list --project backend/RollRadar.Api
```

## 3. Testar

1. Banco no ar (`docker compose up -d`) e migration aplicada:
   `dotnet ef database update --project backend/RollRadar.Api`.
2. Suba a API e o site:
   `dotnet run --project backend/RollRadar.Api --launch-profile http` e `npm --prefix frontend start`.
3. Abra <http://localhost:4200>, clique em **Entrar com Google**, escolha a conta e confirme.
4. Você volta para a mesma página com o seu nome no topo. <http://localhost:4200/api/eu> mostra
   `{ id, email, nome }`; **Sair** encerra a sessão.

## Problemas comuns

- **`redirect_uri_mismatch`**: a URI da mensagem de erro do Google tem que estar exatamente na lista do passo 1.3
  (http, `localhost`, porta e caminho iguais).
- **`access_denied` / "app não verificado"**: o e-mail não está em "Usuários de teste".
- **503 no login**: a API não achou `Auth:Google:ClientId` e `Auth:Google:ClientSecret`; confira o
  `dotnet user-secrets list` e reinicie a API.

## Produção (depois)

Para o site publicado será preciso cadastrar a URI `https://<domínio>/api/auth/google-callback` no mesmo client
(ou em outro), configurar as duas chaves como variáveis de ambiente do servidor (`Auth__Google__ClientId` e
`Auth__Google__ClientSecret`) e garantir que o proxy repasse o domínio do site para a API
(`X-Forwarded-Host`/`X-Forwarded-Proto`), senão o Google devolve para o endereço interno da API.
