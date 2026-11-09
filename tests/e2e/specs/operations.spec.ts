import { expect, test, type Page } from '@playwright/test';

const valetEmail = process.env.TEST_VALET_EMAIL ?? 'manobrista@local.example.invalid';
const cashierEmail = process.env.TEST_CASHIER_EMAIL ?? 'caixa@local.example.invalid';
const administratorEmail = process.env.TEST_ADMIN_EMAIL ?? 'admin@local.example.invalid';

const valetPassword = () => requiredPassword('TEST_VALET_PASSWORD');
const cashierPassword = () => requiredPassword('TEST_CASHIER_PASSWORD');
const administratorPassword = () => requiredPassword('TEST_ADMIN_PASSWORD');

test('cashier completes the full check-in, withdrawal, payment and delivery journey with profile projections', async ({ page }) => {
  test.setTimeout(120_000);

  const suffix = uniqueSuffix();
  const spotCode = `V${suffix}`;
  const plate = `E2E${suffix}`;
  const document = '12345678900';
  const model = 'Sedan E2E';

  await login(page, administratorEmail, administratorPassword());
  await configureOperation(page, spotCode);
  await logout(page);

  await login(page, cashierEmail, cashierPassword());
  await page.getByRole('link', { name: 'Entrada' }).click();
  await expect(page.getByRole('heading', { name: 'Registrar check-in' })).toBeVisible();
  await page.getByLabel('Vaga livre').selectOption({ label: spotCode });
  await page.getByLabel('Placa').fill(plate);
  await page.getByLabel('Modelo').fill(model);
  await page.getByLabel('Titular').fill('Maria de Teste');
  await page.getByLabel('Documento', { exact: true }).fill(document);
  await page.getByLabel('Vistoria de entrada').fill('Sem avarias aparentes.');
  await page.getByRole('button', { name: 'Registrar entrada' }).click();
  await expect(page.getByRole('heading', { name: 'Entrada registrada' })).toBeVisible();
  const publicId = (await page.locator('section[role="status"] strong').first().textContent())?.trim();
  expect(publicId).toBeTruthy();
  expect(publicId).toMatch(/^[A-Z0-9-]+$/);
  await logout(page);

  await login(page, valetEmail, valetPassword());
  await expect(page.getByRole('link', { name: 'Entrada' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Administração' })).toHaveCount(0);
  await page.goto('/admin');
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('heading', { name: 'Visão da operação' })).toBeVisible();

  await page.getByRole('link', { name: 'Buscar', exact: true }).click();
  await page.getByLabel('Placa').fill(plate);
  await page.getByRole('button', { name: 'Buscar' }).click();
  await expect(page.getByRole('heading', { name: `${plate} · ${model}` })).toBeVisible();
  await expect(page.getByText('***00')).toBeVisible();
  await expect(page.getByText(document, { exact: true })).toHaveCount(0);
  await expect(page.getByText('Valores financeiros disponíveis apenas para caixa e administração.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Registrar pedido' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Registrar pagamento' })).toHaveCount(0);
  await logout(page);

  await login(page, cashierEmail, cashierPassword());
  await searchAttendance(page, plate);
  await expect(page.getByRole('heading', { name: `${plate} · ${model}` })).toBeVisible();
  await expect(page.getByText(document, { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Registrar pedido' }).click();
  await expect(page.getByRole('status')).toContainText('Pedido de retirada registrado.');
  await page.getByRole('button', { name: 'Iniciar busca' }).click();
  await expect(page.getByRole('status')).toContainText('Busca do veículo iniciada.');
  await page.getByRole('button', { name: 'Registrar pagamento' }).click();
  await expect(page.getByRole('status')).toContainText('registrado.');
  await page.getByLabel('Vistoria de saída').fill('Devolvido sem novas avarias.');
  await page.getByRole('button', { name: 'Confirmar entrega' }).click();
  await expect(page.getByRole('status')).toContainText('Veículo entregue.');
  await expect(detailRegion(page).getByText('Entregue', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'Painel' }).click();
  await expect(page.getByRole('heading', { name: 'Visão da operação' })).toBeVisible();
  const spotCard = page.getByRole('listitem').filter({ hasText: spotCode });
  await expect(spotCard).toContainText('Livre');
});

test('administrator authorizes an outstanding balance and records an audited adjustment after delivery', async ({ page }) => {
  test.setTimeout(120_000);

  const suffix = uniqueSuffix();
  const spotCode = `V${suffix}`;
  const plate = `E2E${suffix}`;
  const model = 'Hatch E2E';

  await login(page, administratorEmail, administratorPassword());
  await configureOperation(page, spotCode);
  await logout(page);

  await login(page, cashierEmail, cashierPassword());
  await page.getByRole('link', { name: 'Entrada' }).click();
  await page.getByLabel('Vaga livre').selectOption({ label: spotCode });
  await page.getByLabel('Placa').fill(plate);
  await page.getByLabel('Modelo').fill(model);
  await page.getByLabel('Titular').fill('João de Teste');
  await page.getByLabel('Documento', { exact: true }).fill('98765432100');
  await page.getByLabel('Vistoria de entrada').fill('Pneus em bom estado.');
  await page.getByRole('button', { name: 'Registrar entrada' }).click();
  await expect(page.getByRole('heading', { name: 'Entrada registrada' })).toBeVisible();
  await logout(page);

  await login(page, cashierEmail, cashierPassword());
  await searchAttendance(page, plate);
  await page.getByRole('button', { name: 'Registrar pedido' }).click();
  await expect(page.getByRole('status')).toContainText('Pedido de retirada registrado.');
  await page.getByRole('button', { name: 'Iniciar busca' }).click();
  await expect(page.getByRole('status')).toContainText('Busca do veículo iniciada.');
  await expect(page.getByRole('button', { name: 'Confirmar entrega' })).toHaveCount(0);
  await logout(page);

  await login(page, administratorEmail, administratorPassword());
  await searchAttendance(page, plate);
  await page.getByLabel('Tipo').selectOption({ label: 'Saldo pendente' });
  await page.getByLabel('Justificativa').fill('Cliente autorizado pela administração.');
  await page.getByRole('button', { name: 'Autorizar' }).click();
  await expect(page.getByRole('status')).toContainText('Autorização excepcional registrada.');
  await logout(page);

  await login(page, cashierEmail, cashierPassword());
  await searchAttendance(page, plate);
  await page.getByLabel('Vistoria de saída').fill('Entrega autorizada com saldo pendente.');
  await page.getByRole('button', { name: 'Confirmar entrega' }).click();
  await expect(page.getByRole('status')).toContainText('Veículo entregue.');
  await expect(detailRegion(page).getByText('Entregue', { exact: true })).toBeVisible();
  await expect(detailRegion(page).getByText('Saldo', { exact: true }).locator('..')).toContainText('R$ 35,00');
  await logout(page);

  await login(page, administratorEmail, administratorPassword());
  await searchAttendance(page, plate);
  await page.getByLabel('Valor (negativo para desconto)').fill('-10');
  await page.getByLabel('Motivo').fill('Ajuste pós-entrega registrado em teste.');
  await page.getByRole('button', { name: 'Registrar ajuste' }).click();
  await expect(page.getByRole('status')).toContainText('Ajuste de');
  await expect(page.getByText('Histórico financeiro e exceções')).toBeVisible();
  await expect(page.getByText('Ajuste pós-entrega registrado em teste.')).toBeVisible();

  await page.getByRole('button', { name: 'Carregar auditoria' }).click();
  await expect(page.getByText('ExceptionAuthorized')).toBeVisible();
  await expect(page.getByText('DeliveryCompleted')).toBeVisible();
  await expect(page.getByText('AdjustmentRegistered')).toBeVisible();
});

function detailRegion(page: Page) {
  return page.getByRole('region', { name: 'Detalhe do atendimento' });
}

async function configureOperation(page: Page, spotCode: string): Promise<void> {
  await page.goto('/admin');
  await expect(page.getByRole('heading', { name: 'Configuração da operação' })).toBeVisible();
  await page.getByLabel('Nova tarifa (R$)').fill('35');
  await page.getByRole('button', { name: 'Publicar tarifa' }).click();
  await expect(page.getByRole('status')).toContainText('publicada.');
  await page.getByLabel('Código da vaga').fill(spotCode);
  await page.getByRole('button', { name: 'Cadastrar' }).click();
  await expect(page.getByRole('status')).toContainText(`Vaga ${spotCode} cadastrada.`);
}

async function searchAttendance(page: Page, plate: string): Promise<void> {
  await page.getByRole('link', { name: 'Buscar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Buscar atendimento' })).toBeVisible();
  await page.getByLabel('Placa').fill(plate);
  await page.getByRole('button', { name: 'Buscar' }).click();
  await expect(page.getByRole('heading', { name: 'Resultados' })).toBeVisible();
}

async function login(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill(password);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page.getByText('Sessão ativa')).toBeVisible();
}

async function logout(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Encerrar sessão' }).click();
  await expect(page.getByRole('heading', { name: 'Acesso de funcionário' })).toBeVisible();
}

function uniqueSuffix(): string {
  return `${Date.now().toString(36).slice(-4)}${Math.floor(Math.random() * 36).toString(36)}`.toUpperCase();
}

function requiredPassword(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Set ${name} to a provisioned local synthetic account password.`);
  }

  return value;
}
